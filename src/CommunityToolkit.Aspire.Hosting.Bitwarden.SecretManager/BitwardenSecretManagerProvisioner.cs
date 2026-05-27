#pragma warning disable ASPIREINTERACTION001

using System.Security.Cryptography;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Bitwarden.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

/// <summary>
/// Provisions the declared Bitwarden project and secrets graph during the AppHost deployment pipeline.
/// </summary>
internal sealed class BitwardenSecretManagerProvisioner(
    IBitwardenSecretManagerProviderFactory providerFactory)
{
    /// <summary>
    /// Authenticates with Bitwarden Secrets Manager and sets up the cache paths on the resource.
    /// Must run before <see cref="ProvisionProjectAsync"/> and <see cref="ProvisionSecretsAsync"/>.
    /// </summary>
    public async Task AuthenticateAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        resource.ResetResolvedValues();
        logger.LogDebug("Starting Bitwarden authentication for resource '{ResourceName}'.", resource.Name);

        try
        {
            string remoteProjectName = await resource.GetResolvedRemoteProjectNameAsync(cancellationToken).ConfigureAwait(false);
            resource.ResolvedRemoteProjectName = remoteProjectName;
            logger.LogInformation("Resolved remote project name: {RemoteProjectName}.", remoteProjectName);

            string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
            BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Loaded Bitwarden AppHost cache from '{AppHostCachePath}'.", cacheContext.CachePath);

            string accessToken = await resource.GetResolvedManagementAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            logger.LogDebug("Creating Bitwarden provider with API URL '{ApiUrl}' and Identity URL '{IdentityUrl}'.", resource.GetApiUrlOrDefault(), resource.GetIdentityUrlOrDefault());
            await using IBitwardenSecretManagerProvider provider = providerFactory.Create(resource.GetApiUrlOrDefault(), resource.GetIdentityUrlOrDefault());

            logger.LogDebug("Logging into Bitwarden provider for resource '{ResourceName}' using auth cache '{AppHostAuthCachePath}'.", resource.Name, cacheContext.AuthCachePath);
            try
            {
                provider.Login(accessToken, cacheContext.AuthCachePath);
                logger.LogInformation("Successfully authenticated with Bitwarden Secrets Manager for resource '{ResourceName}'.", resource.Name);
            }
            catch (BitwardenAuthException ex)
            {
                logger.LogError(ex, "Failed to authenticate with Bitwarden Secrets Manager for resource '{ResourceName}'. Verify that the access token is valid and has the necessary permissions.", resource.Name);
                throw new DistributedApplicationException($"Bitwarden authentication failed for resource '{resource.Name}': The provided access token is invalid or lacks the required permissions. Please verify the token and try again.", ex);
            }
        }
        catch (Exception ex) when (ex is not DistributedApplicationException)
        {
            logger.LogError(ex, "Bitwarden authentication failed for resource '{ResourceName}'.", resource.Name);
            throw;
        }
    }

    /// <summary>
    /// Creates or updates the remote Bitwarden project and binds the resolved project ID on the resource.
    /// Requires <see cref="AuthenticateAsync"/> to have completed successfully first.
    /// </summary>
    public async Task ProvisionProjectAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogDebug("Starting Bitwarden project provisioning for resource '{ResourceName}'.", resource.Name);

        try
        {
            string remoteProjectName = resource.ResolvedRemoteProjectName
                ?? await resource.GetResolvedRemoteProjectNameAsync(cancellationToken).ConfigureAwait(false);

            string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
            BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);

            Guid organizationId = await resource.GetResolvedOrganizationIdAsync(cancellationToken).ConfigureAwait(false);
            string accessToken = await resource.GetResolvedManagementAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            await using IBitwardenSecretManagerProvider provider = providerFactory.Create(resource.GetApiUrlOrDefault(), resource.GetIdentityUrlOrDefault());
            provider.Login(accessToken, cacheContext.AuthCachePath);

            BitwardenProjectInfo project = ReconcileProject(resource, remoteProjectName, cacheContext.Cache, provider, organizationId, logger);
            resource.BindResolvedProjectId(project.Id);
            logger.LogInformation("Successfully provisioned project {ProjectId} for resource '{ResourceName}'.", project.Id, resource.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bitwarden project provisioning failed for resource '{ResourceName}'.", resource.Name);
            throw;
        }
    }

    /// <summary>
    /// Creates or updates managed secrets and validates declared secret references, then saves the AppHost cache.
    /// Requires <see cref="ProvisionProjectAsync"/> to have completed successfully first.
    /// </summary>
    public async Task ProvisionSecretsAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogDebug("Starting Bitwarden secrets provisioning for resource '{ResourceName}'.", resource.Name);

        try
        {
            string remoteProjectName = resource.ResolvedRemoteProjectName
                ?? await resource.GetResolvedRemoteProjectNameAsync(cancellationToken).ConfigureAwait(false);

            string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
            BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);

            Guid organizationId = await resource.GetResolvedOrganizationIdAsync(cancellationToken).ConfigureAwait(false);
            string accessToken = await resource.GetResolvedManagementAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            IInteractionService? interactionService = services.GetService<IInteractionService>();

            await using IBitwardenSecretManagerProvider provider = providerFactory.Create(resource.GetApiUrlOrDefault(), resource.GetIdentityUrlOrDefault());
            provider.Login(accessToken, cacheContext.AuthCachePath);

            Dictionary<string, Guid> staleManagedMappings = cacheContext.Cache.ManagedSecretIds
                .Where(entry => resource.ManagedSecrets.All(secret => !string.Equals(secret.LocalName, entry.Key, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

            if (staleManagedMappings.Count > 0)
            {
                logger.LogInformation("Found {StaleSecretCount} stale managed secret mappings that will be cleaned up.", staleManagedMappings.Count);
            }

            BitwardenLookupContext lookupContext = new(provider, organizationId);

            logger.LogInformation("Provisioning {ManagedSecretCount} managed secrets for resource '{ResourceName}'.", resource.ManagedSecrets.Count, resource.Name);
            foreach (BitwardenSecretResource secret in resource.ManagedSecrets)
            {
                logger.LogDebug("Processing managed secret '{SecretName}' (remote name: {RemoteName}).", secret.LocalName, secret.RemoteName);
                await ReconcileManagedSecretAsync(resource, organizationId, secret, cacheContext.Cache, lookupContext, provider, interactionService, logger, cancellationToken, staleManagedMappings).ConfigureAwait(false);
            }

            cacheContext.Cache.ManagedSecretIds = resource.ManagedSecrets
                .Where(secret => secret.SecretId is not null)
                .ToDictionary(secret => secret.LocalName, secret => secret.SecretId!.Value, StringComparer.OrdinalIgnoreCase);

            logger.LogInformation("Validating {DeclaredSecretCount} declared secret references for resource '{ResourceName}'.", resource.DeclaredSecretReferences.Count, resource.Name);
            await ValidateDeclaredSecretReferencesAsync(resource, cacheContext.Cache, lookupContext, interactionService, logger, cancellationToken).ConfigureAwait(false);

            cacheContext.Cache.ProjectId = resource.ProjectId;

            logger.LogDebug("Saving Bitwarden state file to '{StatePath}'.", cacheContext.CachePath);
            await BitwardenStore.SaveAsync(cacheContext.CachePath, cacheContext.Cache, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Successfully saved Bitwarden state file.");

            logger.LogInformation("Bitwarden secrets provisioning completed for resource '{ResourceName}' with project {ProjectId}.", resource.Name, resource.ProjectId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bitwarden secrets provisioning failed for resource '{ResourceName}'.", resource.Name);
            throw;
        }
    }

    private static BitwardenProjectInfo ReconcileProject(
        BitwardenSecretManagerResource resource,
        string remoteProjectName,
        BitwardenCache cache,
        IBitwardenSecretManagerProvider provider,
        Guid organizationId,
        ILogger logger)
    {
        if (resource.ExistingProjectId is Guid existingProjectId)
        {
            logger.LogInformation("Attempting to use explicitly configured project {ProjectId} for resource '{ResourceName}'.", existingProjectId, resource.Name);
            BitwardenProjectInfo? existingProject = provider.GetProject(existingProjectId);
            if (existingProject is null)
            {
                logger.LogError("Configured project {ProjectId} was not found for resource '{ResourceName}'.", existingProjectId, resource.Name);
                throw new DistributedApplicationException($"Bitwarden project '{existingProjectId:D}' configured for resource '{resource.Name}' was not found.");
            }

            logger.LogInformation("Using existing Bitwarden project {ProjectId} for resource {ResourceName}.", existingProject.Id, resource.Name);
            return existingProject;
        }

        if (cache.ProjectId is Guid persistedProjectId)
        {
            logger.LogDebug("Attempting to reuse persisted project {ProjectId} from state file for resource '{ResourceName}'.", persistedProjectId, resource.Name);
            BitwardenProjectInfo? persistedProject = provider.GetProject(persistedProjectId);
            if (persistedProject is not null)
            {
                if (!string.Equals(persistedProject.Name, remoteProjectName, StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "Bitwarden project {ProjectId} name drifted to '{CurrentProjectName}'; updating to '{DesiredProjectName}' for resource {ResourceName}.",
                        persistedProject.Id,
                        persistedProject.Name,
                        remoteProjectName,
                        resource.Name);

                    return provider.UpdateProject(organizationId, persistedProject.Id, remoteProjectName);
                }

                logger.LogInformation("Using persisted Bitwarden project {ProjectId} for resource {ResourceName}.", persistedProject.Id, resource.Name);
                return persistedProject;
            }

            logger.LogWarning(
                "Persisted Bitwarden project {ProjectId} for resource '{ResourceName}' was not found. A new project will be created.",
                persistedProjectId,
                resource.Name);
        }

        logger.LogInformation("Creating new Bitwarden project '{ProjectName}' for resource '{ResourceName}' in organization {OrganizationId}.", remoteProjectName, resource.Name, organizationId);
        return provider.CreateProject(organizationId, remoteProjectName);
    }

    private static async Task ReconcileManagedSecretAsync(
        BitwardenSecretManagerResource resource,
        Guid organizationId,
        BitwardenSecretResource secretResource,
        BitwardenCache state,
        BitwardenLookupContext lookupContext,
        IBitwardenSecretManagerProvider provider,
        IInteractionService? interactionService,
        ILogger logger,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, Guid> staleManagedMappings)
    {
        logger.LogDebug("Resolving value for managed secret '{SecretName}'.", secretResource.LocalName);
        string resolvedValue = await ResolveSecretValueAsync(resource, secretResource.Value, secretResource.LocalName, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Successfully resolved value for managed secret '{SecretName}'.", secretResource.LocalName);

        Guid projectId = resource.ProjectId ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' has not resolved a project identifier.");

        BitwardenSecretInfo secret;
        if (secretResource.ExistingSecretId is Guid explicitSecretId)
        {
            logger.LogDebug("Using explicitly configured secret ID {SecretId} for managed secret '{SecretName}'.", explicitSecretId, secretResource.LocalName);
            BitwardenSecretInfo? explicitSecret = lookupContext.GetSecret(explicitSecretId);
            if (explicitSecret is null)
            {
                logger.LogError("Configured secret {SecretId} was not found for managed secret '{SecretName}'.", explicitSecretId, secretResource.LocalName);
                throw new DistributedApplicationException($"Bitwarden secret '{explicitSecretId:D}' configured for managed secret '{secretResource.LocalName}' was not found.");
            }

            logger.LogDebug("Ensuring configured secret {SecretId} matches desired configuration for managed secret '{SecretName}'.", explicitSecretId, secretResource.LocalName);
            secret = EnsureSecretMatches(provider, explicitSecret, projectId, secretResource.RemoteName, resolvedValue);
        }
        else if (state.ManagedSecretIds.TryGetValue(secretResource.LocalName, out Guid persistedSecretId))
        {
            logger.LogDebug("Found persisted secret ID {SecretId} for managed secret '{SecretName}'.", persistedSecretId, secretResource.LocalName);
            BitwardenSecretInfo? persistedSecret = lookupContext.GetSecret(persistedSecretId);
            if (persistedSecret is null || persistedSecret.ProjectId != projectId)
            {
                logger.LogWarning(
                    "Managed Bitwarden secret '{SecretName}' (remote: {RemoteName}) has drifted out of project {ProjectId}. A replacement secret will be created.",
                    secretResource.LocalName,
                    secretResource.RemoteName,
                    projectId);

                secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId]);
                logger.LogInformation("Created replacement secret {SecretId} for managed secret '{SecretName}'.", secret.Id, secretResource.LocalName);
            }
            else
            {
                logger.LogDebug("Ensuring persisted secret {SecretId} matches desired configuration for managed secret '{SecretName}'.", persistedSecretId, secretResource.LocalName);
                secret = EnsureSecretMatches(provider, persistedSecret, projectId, secretResource.RemoteName, resolvedValue);
            }
        }
        else
        {
            logger.LogDebug("Searching for existing secrets named '{RemoteName}' in project {ProjectId} for managed secret '{SecretName}'.", secretResource.RemoteName, projectId, secretResource.LocalName);
            IReadOnlyList<BitwardenSecretInfo> candidates = lookupContext.FindSecretsByNameInProject(secretResource.RemoteName, projectId);

            if (candidates.Count == 0)
            {
                logger.LogInformation("No existing secret found for managed secret '{SecretName}' (remote: {RemoteName}). Creating new secret.", secretResource.LocalName, secretResource.RemoteName);
                secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId]);
                logger.LogInformation("Created new secret {SecretId} for managed secret '{SecretName}'.", secret.Id, secretResource.LocalName);
            }
            else if (candidates.Count == 1)
            {
                if (HasHistoricalManagedMapping(staleManagedMappings, lookupContext, secretResource.RemoteName))
                {
                    logger.LogWarning(
                        "Creating a new Bitwarden secret for managed secret '{SecretName}' because the previous local identity was renamed and no explicit adoption was configured.",
                        secretResource.LocalName);

                    secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId]);
                    logger.LogInformation("Created new secret {SecretId} for renamed managed secret '{SecretName}'.", secret.Id, secretResource.LocalName);
                }
                else
                {
                    logger.LogDebug("Ensuring single matching secret {SecretId} matches desired configuration for managed secret '{SecretName}'.", candidates[0].Id, secretResource.LocalName);
                    secret = EnsureSecretMatches(provider, candidates[0], projectId, secretResource.RemoteName, resolvedValue);
                }
            }
            else
            {
                logger.LogWarning(
                    "Found {CandidateCount} existing secrets named '{RemoteName}' in project {ProjectId} for managed secret '{SecretName}'. User interaction required to resolve.",
                    candidates.Count,
                    secretResource.RemoteName,
                    projectId,
                    secretResource.LocalName);

                Guid selectedSecretId = await ResolveDuplicateAsync(
                    interactionService,
                    resource,
                    secretResource.RemoteName,
                    candidates,
                    cancellationToken).ConfigureAwait(false);

                logger.LogInformation("User selected secret {SecretId} for managed secret '{SecretName}'.", selectedSecretId, secretResource.LocalName);
                BitwardenSecretInfo selectedSecret = candidates.Single(candidate => candidate.Id == selectedSecretId);
                secret = EnsureSecretMatches(provider, selectedSecret, projectId, secretResource.RemoteName, resolvedValue);
            }
        }

        lookupContext.CacheSecret(secret);
        secretResource.SecretId = secret.Id;
        resource.BindResolvedSecret(secret.Id, secretResource.RemoteName, secret.Value);
        logger.LogInformation("Successfully provisioned managed secret '{SecretName}' with ID {SecretId}.", secretResource.LocalName, secret.Id);
    }

    private static async Task ValidateDeclaredSecretReferencesAsync(
        BitwardenSecretManagerResource resource,
        BitwardenCache state,
        BitwardenLookupContext lookupContext,
        IInteractionService? interactionService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Guid projectId = resource.ProjectId ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' has not resolved a project identifier.");

        foreach (IBitwardenSecretReference secretReference in resource.DeclaredSecretReferences)
        {
            if (secretReference.SecretOwner is BitwardenSecretResource managedSecret)
            {
                logger.LogDebug("Processing declared reference to managed secret '{SecretName}'.", managedSecret.LocalName);
                if (managedSecret.SecretId is Guid managedSecretId)
                {
                    string? managedSecretValue = resource.ResolveSecretValue(managedSecret);
                    if (managedSecretValue is not null)
                    {
                        resource.BindResolvedSecret(managedSecretId, managedSecret.RemoteName, managedSecretValue);
                        logger.LogDebug("Bound declared reference to managed secret {SecretId} for '{SecretName}'.", managedSecretId, managedSecret.LocalName);
                    }
                }

                continue;
            }

            if (secretReference.SecretId is Guid explicitSecretId)
            {
                logger.LogDebug("Processing declared reference to explicit secret {SecretId}.", explicitSecretId);
                BitwardenSecretInfo? secret = lookupContext.GetSecret(explicitSecretId);
                if (secret is null)
                {
                    logger.LogError("Declared secret reference {SecretId} was not found.", explicitSecretId);
                    throw new DistributedApplicationException($"Bitwarden secret '{explicitSecretId:D}' referenced by resource '{resource.Name}' was not found.");
                }

                if (secret.ProjectId != projectId)
                {
                    logger.LogError("Declared secret reference {SecretId} does not belong to project {ProjectId}.", explicitSecretId, projectId);
                    throw new DistributedApplicationException($"Bitwarden secret '{explicitSecretId:D}' referenced by resource '{resource.Name}' does not belong to Bitwarden project '{projectId:D}'.");
                }

                resource.BindResolvedSecret(secret.Id, secret.Key, secret.Value);
                logger.LogDebug("Bound declared reference to explicit secret {SecretId} ({SecretName}).", secret.Id, secret.Key);
                continue;
            }

            string remoteName = secretReference.RemoteName ?? throw new DistributedApplicationException($"Bitwarden secret reference in resource '{resource.Name}' did not specify a secret name or identifier.");
            logger.LogDebug("Processing declared reference to secret named '{RemoteName}'.", remoteName);
            BitwardenSecretInfo secretByName;

            if (state.NameBindings.TryGetValue(remoteName, out Guid persistedSecretId))
            {
                logger.LogDebug("Found persisted binding for secret name '{RemoteName}': {SecretId}.", remoteName, persistedSecretId);
                BitwardenSecretInfo? persistedSecret = lookupContext.GetSecret(persistedSecretId);
                if (persistedSecret is not null && persistedSecret.ProjectId == projectId)
                {
                    if (!string.Equals(persistedSecret.Key, remoteName, StringComparison.Ordinal))
                    {
                        logger.LogWarning(
                            "Using persisted binding {SecretId} for remote name '{RemoteName}' even though the remote secret is currently named '{CurrentRemoteName}'.",
                            persistedSecret.Id,
                            remoteName,
                            persistedSecret.Key);
                    }

                    resource.BindResolvedSecret(persistedSecret.Id, remoteName, persistedSecret.Value);
                    logger.LogDebug("Bound declared reference to persisted secret {SecretId} for name '{RemoteName}'.", persistedSecret.Id, remoteName);
                    continue;
                }

                logger.LogWarning(
                    "Persisted binding {SecretId} for remote name '{RemoteName}' is no longer valid. The binding will be re-resolved.",
                    persistedSecretId,
                    remoteName);
            }

            logger.LogDebug("Searching for secrets named '{RemoteName}' in project {ProjectId}.", remoteName, projectId);
            IReadOnlyList<BitwardenSecretInfo> candidates = lookupContext.FindSecretsByNameInProject(remoteName, projectId);
            if (candidates.Count == 0)
            {
                logger.LogError("No Bitwarden secret named '{RemoteName}' found in project {ProjectId} for resource '{ResourceName}'.", remoteName, projectId, resource.Name);
                throw new DistributedApplicationException($"Bitwarden secret '{remoteName}' referenced by resource '{resource.Name}' was not found in Bitwarden project '{projectId:D}'.");
            }

            if (candidates.Count == 1)
            {
                secretByName = candidates[0];
                logger.LogDebug("Found single matching secret {SecretId} for name '{RemoteName}'.", secretByName.Id, remoteName);
            }
            else
            {
                logger.LogWarning(
                    "Found {CandidateCount} secrets named '{RemoteName}' in project {ProjectId}. User interaction required to resolve.",
                    candidates.Count,
                    remoteName,
                    projectId);

                Guid selectedSecretId = await ResolveDuplicateAsync(interactionService, resource, remoteName, candidates, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("User selected secret {SecretId} for declared reference to '{RemoteName}'.", selectedSecretId, remoteName);
                secretByName = candidates.Single(candidate => candidate.Id == selectedSecretId);
            }

            state.NameBindings[remoteName] = secretByName.Id;
            resource.BindResolvedSecret(secretByName.Id, remoteName, secretByName.Value);
            logger.LogInformation("Successfully resolved declared reference to secret {SecretId} for name '{RemoteName}'.", secretByName.Id, remoteName);
        }
    }

    private static BitwardenSecretInfo EnsureSecretMatches(
        IBitwardenSecretManagerProvider provider,
        BitwardenSecretInfo secret,
        Guid managedProjectId,
        string remoteName,
        string value)
    {
        bool requiresProjectUpdate = secret.ProjectId != managedProjectId;
        bool requiresNameUpdate = !string.Equals(secret.Key, remoteName, StringComparison.Ordinal);
        bool requiresValueUpdate = !string.Equals(secret.Value, value, StringComparison.Ordinal);

        if (!requiresProjectUpdate && !requiresNameUpdate && !requiresValueUpdate)
        {
            return secret;
        }

        Guid[] projectIds = BuildProjectIds(secret.ProjectId, managedProjectId);
        return provider.UpdateSecret(secret.OrganizationId, secret.Id, remoteName, value, secret.Note, projectIds);
    }

    private static Guid[] BuildProjectIds(Guid? existingProjectId, Guid managedProjectId)
    {
        List<Guid> projectIds = [managedProjectId];
        if (existingProjectId is Guid existing && existing != managedProjectId)
        {
            projectIds.Add(existing);
        }

        return [.. projectIds];
    }

    private static bool HasHistoricalManagedMapping(
        IReadOnlyDictionary<string, Guid> staleManagedMappings,
        BitwardenLookupContext lookupContext,
        string remoteName)
    {
        foreach ((_, Guid secretId) in staleManagedMappings)
        {
            BitwardenSecretInfo? secret = lookupContext.GetSecret(secretId);
            if (secret is null)
            {
                continue;
            }

            if (string.Equals(secret.Key, remoteName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string> ResolveSecretValueAsync(
        BitwardenSecretManagerResource resource,
        object valueSource,
        string secretName,
        CancellationToken cancellationToken)
    {
        string? value = valueSource switch
        {
            ParameterResource parameter => await ResolveRequiredParameterValueAsync(
                parameter,
                resource,
                $"managed secret '{secretName}'",
                cancellationToken).ConfigureAwait(false),
            ReferenceExpression referenceExpression => await referenceExpression.GetValueAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new DistributedApplicationException($"Managed Bitwarden secret '{secretName}' uses unsupported value source type '{valueSource.GetType().Name}'.")
        };

        if (value is null)
        {
            throw new DistributedApplicationException($"Managed Bitwarden secret '{secretName}' did not resolve to a value.");
        }

        return value;
    }

    private static async Task<string> ResolveRequiredParameterValueAsync(
        ParameterResource parameter,
        BitwardenSecretManagerResource resource,
        string purpose,
        CancellationToken cancellationToken)
    {
        string? configuredValue = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            throw new DistributedApplicationException(
                $"Bitwarden {purpose} parameter '{parameter.Name}' for resource '{resource.Name}' did not resolve to a value.");
        }

        return configuredValue;
    }

    private static async Task<Guid> ResolveDuplicateAsync(
        IInteractionService? interactionService,
        BitwardenSecretManagerResource resource,
        string remoteName,
        IReadOnlyList<BitwardenSecretInfo> candidates,
        CancellationToken cancellationToken)
    {
        string candidateIds = string.Join(Environment.NewLine, candidates.Select(candidate => $"- {candidate.Id:D}"));

        if (interactionService is null || !interactionService.IsAvailable)
        {
            throw new DistributedApplicationException(
                $"Bitwarden resource '{resource.Name}' resolved multiple secrets named '{remoteName}' in project '{resource.ProjectId:D}'. Resolve the duplicate remotely or rerun with interactive prompts enabled. Candidates:{Environment.NewLine}{candidateIds}");
        }

        InteractionInput input = new()
        {
            Name = "secretId",
            Label = $"Bitwarden secret ID for '{remoteName}'",
            InputType = InputType.Text,
            Required = true,
            Value = candidates[0].Id.ToString("D")
        };

        InteractionResult<InteractionInput> result = await interactionService.PromptInputAsync(
            $"Resolve duplicate Bitwarden secret '{remoteName}'",
            $"Multiple Bitwarden secrets named '{remoteName}' were found for resource '{resource.Name}'. Enter one of the following IDs:{Environment.NewLine}{candidateIds}",
            input,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Canceled || result.Data is null)
        {
            throw new DistributedApplicationException($"Bitwarden duplicate resolution for secret '{remoteName}' was canceled.");
        }

        string? selectedValue = result.Data.Value;
        if (!Guid.TryParse(selectedValue, out Guid selectedSecretId) || !candidates.Any(candidate => candidate.Id == selectedSecretId))
        {
            throw new DistributedApplicationException($"'{selectedValue}' is not a valid Bitwarden secret selection for duplicate secret '{remoteName}'.");
        }

        return selectedSecretId;
    }

    public static async Task ResetAuthCacheAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);

        string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
        if (File.Exists(authCachePath))
        {
            File.Delete(authCachePath);
        }
    }

    private static async Task<string> ResolveAuthCachePathAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (resource.AuthCacheFile is { Length: > 0 } authCacheFile)
        {
            if (Path.IsPathRooted(authCacheFile))
            {
                return authCacheFile;
            }

            IAspireStore aspireStore = services.GetRequiredService<IAspireStore>();
            return Path.GetFullPath(Path.Combine(aspireStore.BasePath, authCacheFile));
        }

        // Key the default auth cache on the access token value so that rotating the token
        // automatically starts a fresh session, and different tokens never share a session file.
        string? accessToken = await resource.ManagementAccessToken.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new DistributedApplicationException($"Bitwarden access token for resource '{resource.Name}' did not resolve to a value.");
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
        string tokenHash = Convert.ToHexString(hash).ToLowerInvariant()[..7];

        IAspireStore store = services.GetRequiredService<IAspireStore>();
        return Path.Combine(store.BasePath, ".bitwarden", $"{tokenHash}.auth-cache");
    }
}

internal sealed class BitwardenLookupContext(IBitwardenSecretManagerProvider provider, Guid organizationId)
{
    private IReadOnlyList<BitwardenSecretIdentifierInfo>? _secretIdentifiers;
    private readonly Dictionary<Guid, BitwardenSecretInfo?> _secretsById = [];

    public BitwardenSecretInfo? GetSecret(Guid secretId)
    {
        if (_secretsById.TryGetValue(secretId, out BitwardenSecretInfo? cachedSecret))
        {
            return cachedSecret;
        }

        BitwardenSecretInfo? secret = provider.GetSecret(secretId);
        _secretsById[secretId] = secret;
        return secret;
    }

    public IReadOnlyList<BitwardenSecretInfo> FindSecretsByNameInProject(string remoteName, Guid projectId)
    {
        _secretIdentifiers ??= provider.ListSecrets(organizationId);

        Guid[] secretIds = _secretIdentifiers
            .Where(secret => string.Equals(secret.Key, remoteName, StringComparison.OrdinalIgnoreCase))
            .Select(secret => secret.Id)
            .ToArray();

        if (secretIds.Length == 0)
        {
            return [];
        }

        Guid[] missingSecretIds = secretIds.Where(secretId => !_secretsById.ContainsKey(secretId)).ToArray();
        if (missingSecretIds.Length > 0)
        {
            foreach (BitwardenSecretInfo secret in provider.GetSecretsByIds(missingSecretIds))
            {
                _secretsById[secret.Id] = secret;
            }

            foreach (Guid missingSecretId in missingSecretIds.Where(secretId => !_secretsById.ContainsKey(secretId)))
            {
                _secretsById[missingSecretId] = null;
            }
        }

        return secretIds
            .Select(secretId => _secretsById[secretId])
            .Where(secret => secret is not null && secret.ProjectId == projectId && string.Equals(secret.Key, remoteName, StringComparison.OrdinalIgnoreCase))
            .Cast<BitwardenSecretInfo>()
            .ToArray();
    }

    public void CacheSecret(BitwardenSecretInfo secret)
    {
        _secretsById[secret.Id] = secret;
    }
}
