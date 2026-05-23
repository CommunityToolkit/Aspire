#pragma warning disable ASPIREINTERACTION001

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Bitwarden.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

/// <summary>
/// Reconciles the declared Bitwarden graph during AppHost startup.
/// </summary>
internal sealed class BitwardenSecretManagerReconciler(
    IBitwardenSecretManagerProviderFactory providerFactory,
    BitwardenStateStore stateStore)
{
    public async Task<BitwardenReconciliationResult> InitializeAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        resource.ResetResolvedValues();
        logger.LogDebug("Starting Bitwarden SecretManager initialization for resource '{ResourceName}'.", resource.Name);

        try
        {
            IInteractionService? interactionService = services.GetService<IInteractionService>();

            logger.LogDebug("Resolving organization ID for resource '{ResourceName}'.", resource.Name);
            Guid organizationId = await ResolveOrganizationIdAsync(resource, interactionService, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Resolved organization ID: {OrganizationId}.", organizationId);

            logger.LogDebug("Resolving management access token for resource '{ResourceName}'.", resource.Name);
            string accessToken = await ResolveManagementAccessTokenAsync(resource, interactionService, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Successfully resolved management access token.");

            logger.LogDebug("Resolving remote project name for resource '{ResourceName}'.", resource.Name);
            string remoteProjectName = await ResolveProjectNameAsync(resource, interactionService, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Resolved remote project name: {RemoteProjectName}.", remoteProjectName);
            resource.ResolvedRemoteProjectName = remoteProjectName;

            logger.LogDebug("Loading Bitwarden reconciliation state file for resource '{ResourceName}' with project name '{ProjectName}'.", resource.Name, remoteProjectName);
            BitwardenStateFileContext stateContext = await stateStore.LoadAsync(resource, remoteProjectName, cancellationToken).ConfigureAwait(false);
            resource.ResolvedStateFile = stateContext.Path;
            logger.LogInformation("Loaded Bitwarden reconciliation state file from '{StatePath}'.", stateContext.Path);

            logger.LogDebug("Creating Bitwarden provider with API URL '{ApiUrl}' and Identity URL '{IdentityUrl}'.", resource.GetApiUrlOrDefault(), resource.GetIdentityUrlOrDefault());
            await using IBitwardenSecretManagerProvider provider = providerFactory.Create(resource.GetApiUrlOrDefault(), resource.GetIdentityUrlOrDefault());

            logger.LogDebug("Logging into Bitwarden provider for resource '{ResourceName}' using auth state file '{AuthStatePath}'.", resource.Name, stateContext.AuthPath);
            try
            {
                provider.Login(accessToken, stateContext.AuthPath);
                logger.LogDebug("Successfully authenticated with Bitwarden provider.");
            }
            catch (BitwardenAuthException ex)
            {
                logger.LogError(ex, "Failed to authenticate with Bitwarden provider for resource '{ResourceName}'. Verify that the access token is valid and has the necessary permissions.", resource.Name);
                throw new DistributedApplicationException($"Bitwarden authentication failed for resource '{resource.Name}': The provided access token is invalid or lacks the required permissions. Please verify the token and try again.", ex);
            }

            logger.LogDebug("Reconciling Bitwarden project for resource '{ResourceName}'.", resource.Name);
            BitwardenProjectInfo project = ReconcileProject(resource, remoteProjectName, stateContext.State, provider, organizationId, logger);
            resource.BindResolvedProjectId(project.Id);
            logger.LogInformation("Successfully reconciled project {ProjectId} for resource '{ResourceName}'.", project.Id, resource.Name);

            Dictionary<string, Guid> staleManagedMappings = stateContext.State.ManagedSecretIds
                .Where(entry => resource.ManagedSecrets.All(secret => !string.Equals(secret.LocalName, entry.Key, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

            if (staleManagedMappings.Count > 0)
            {
                logger.LogInformation("Found {StaleSecretCount} stale managed secret mappings that will be cleaned up.", staleManagedMappings.Count);
            }

            BitwardenLookupContext lookupContext = new(provider, organizationId);

            logger.LogInformation("Reconciling {ManagedSecretCount} managed secrets for resource '{ResourceName}'.", resource.ManagedSecrets.Count, resource.Name);
            foreach (BitwardenSecretResource secret in resource.ManagedSecrets)
            {
                logger.LogDebug("Processing managed secret '{SecretName}' (remote name: {RemoteName}).", secret.LocalName, secret.RemoteName);
                await ReconcileManagedSecretAsync(resource, organizationId, secret, stateContext.State, lookupContext, provider, interactionService, logger, cancellationToken, staleManagedMappings).ConfigureAwait(false);
            }

            stateContext.State.ManagedSecretIds = resource.ManagedSecrets
                .Where(secret => secret.SecretId is not null)
                .ToDictionary(secret => secret.LocalName, secret => secret.SecretId!.Value, StringComparer.OrdinalIgnoreCase);

            logger.LogInformation("Validating {DeclaredSecretCount} declared secret references for resource '{ResourceName}'.", resource.DeclaredSecretReferences.Count, resource.Name);
            await ValidateDeclaredSecretReferencesAsync(resource, stateContext.State, lookupContext, interactionService, logger, cancellationToken).ConfigureAwait(false);

            stateContext.State.ProjectId = project.Id;

            logger.LogDebug("Saving Bitwarden state file to '{StatePath}'.", stateContext.Path);
            await stateStore.SaveAsync(stateContext.Path, stateContext.State, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Successfully saved Bitwarden state file.");

            logger.LogInformation("Bitwarden SecretManager initialization completed successfully for resource '{ResourceName}' with project {ProjectId}.", resource.Name, project.Id);
            return new BitwardenReconciliationResult(project.Id, stateContext.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bitwarden SecretManager initialization failed for resource '{ResourceName}'.", resource.Name);
            throw;
        }
    }

    private static BitwardenProjectInfo ReconcileProject(
        BitwardenSecretManagerResource resource,
        string remoteProjectName,
        BitwardenState state,
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

        if (state.ProjectId is Guid persistedProjectId)
        {
            logger.LogDebug("Attempting to reuse persisted project {ProjectId} from state file for resource '{ResourceName}'.", persistedProjectId, resource.Name);
            BitwardenProjectInfo? persistedProject = provider.GetProject(persistedProjectId);
            if (persistedProject is not null)
            {
                if (!string.Equals(persistedProject.Name, remoteProjectName, StringComparison.Ordinal))
                {
                    logger.LogInformation(
                        "Updating Bitwarden project {ProjectId} name from '{CurrentProjectName}' to '{DesiredProjectName}' for resource {ResourceName}.",
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
        BitwardenState state,
        BitwardenLookupContext lookupContext,
        IBitwardenSecretManagerProvider provider,
        IInteractionService? interactionService,
        ILogger logger,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, Guid> staleManagedMappings)
    {
        logger.LogDebug("Resolving value for managed secret '{SecretName}'.", secretResource.LocalName);
        string resolvedValue = await ResolveSecretValueAsync(resource, secretResource.Value, secretResource.LocalName, interactionService, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Successfully resolved value for managed secret '{SecretName}'.", secretResource.LocalName);

        Guid projectId = resource.ProjectId ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' has not resolved a project identifier.");

        BitwardenSecretInfo secret;
        if (state.ManagedSecretIds.TryGetValue(secretResource.LocalName, out Guid persistedSecretId))
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
        else if (secretResource.ExistingSecretId is Guid explicitSecretId)
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
                    logger.LogInformation(
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
        logger.LogInformation("Successfully reconciled managed secret '{SecretName}' with ID {SecretId}.", secretResource.LocalName, secret.Id);
    }

    private static async Task ValidateDeclaredSecretReferencesAsync(
        BitwardenSecretManagerResource resource,
        BitwardenState state,
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
        IInteractionService? interactionService,
        CancellationToken cancellationToken)
    {
        string? value = valueSource switch
        {
            ParameterResource parameter => await ResolveRequiredParameterValueAsync(
                parameter,
                resource,
                $"managed secret '{secretName}'",
                interactionService,
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

    private static async Task<Guid> ResolveOrganizationIdAsync(
        BitwardenSecretManagerResource resource,
        IInteractionService? interactionService,
        CancellationToken cancellationToken)
    {
        if (resource.ConfiguredOrganizationId is Guid literalOrganizationId)
        {
            return literalOrganizationId;
        }

        ParameterResource organizationParameter = resource.ConfiguredOrganizationIdParameter
            ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' does not have an organization configured.");

        string organizationValue = await ResolveRequiredParameterValueAsync(
            organizationParameter,
            resource,
            "organization ID",
            interactionService,
            cancellationToken).ConfigureAwait(false);

        if (!Guid.TryParse(organizationValue, out Guid organizationId))
        {
            throw new DistributedApplicationException(
                $"Bitwarden organization parameter '{organizationParameter.Name}' for resource '{resource.Name}' did not resolve to a valid GUID.");
        }

        return organizationId;
    }

    private static async Task<string> ResolveProjectNameAsync(
        BitwardenSecretManagerResource resource,
        IInteractionService? interactionService,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(resource.RemoteProjectName))
        {
            return resource.RemoteProjectName;
        }

        ParameterResource projectNameParameter = resource.ConfiguredRemoteProjectNameParameter
            ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' does not have a project name configured.");

        string projectName = await ResolveRequiredParameterValueAsync(
            projectNameParameter,
            resource,
            "project name",
            interactionService,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new DistributedApplicationException(
                $"Bitwarden project name parameter '{projectNameParameter.Name}' for resource '{resource.Name}' did not resolve to a value.");
        }

        return projectName;
    }

    private static Task<string> ResolveManagementAccessTokenAsync(
        BitwardenSecretManagerResource resource,
        IInteractionService? interactionService,
        CancellationToken cancellationToken)
    {
        return ResolveRequiredParameterValueAsync(
            resource.ManagementAccessToken,
            resource,
            "management access token",
            interactionService,
            cancellationToken);
    }

    private static async Task<string> ResolveRequiredParameterValueAsync(
        ParameterResource parameter,
        BitwardenSecretManagerResource resource,
        string purpose,
        IInteractionService? interactionService,
        CancellationToken cancellationToken)
    {
        try
        {
            string? configuredValue = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                throw new DistributedApplicationException(
                    $"Bitwarden {purpose} parameter '{parameter.Name}' for resource '{resource.Name}' did not resolve to a value.");
            }

            return configuredValue;
        }
        catch (MissingParameterValueException ex)
        {
            if (interactionService is null || !interactionService.IsAvailable)
            {
                throw new DistributedApplicationException(
                    $"Bitwarden {purpose} parameter '{parameter.Name}' for resource '{resource.Name}' is missing. Configure it under Parameters:{parameter.Name} or run with interactive prompts enabled.",
                    ex);
            }

            InteractionInput input = new()
            {
                Name = parameter.Name,
                Label = $"Bitwarden {purpose}",
                InputType = parameter.Secret ? InputType.SecretText : InputType.Text,
                Required = true
            };

            InteractionResult<InteractionInput> result = await interactionService.PromptInputAsync(
                $"Missing Bitwarden parameter '{parameter.Name}'",
                $"Bitwarden resource '{resource.Name}' requires a value for {purpose}. Enter a value for parameter '{parameter.Name}' to continue.",
                input,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Canceled || result.Data is null || string.IsNullOrWhiteSpace(result.Data.Value))
            {
                throw new DistributedApplicationException(
                    $"Bitwarden parameter prompt for '{parameter.Name}' was canceled or returned an empty value.");
            }

            return result.Data.Value;
        }
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
}

internal sealed record BitwardenReconciliationResult(Guid ProjectId, string StateFile);

internal sealed class BitwardenStateStore(IServiceProvider services)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<BitwardenStateFileContext> LoadAsync(BitwardenSecretManagerResource resource, string resolvedProjectName, CancellationToken cancellationToken)
    {
        string statePath = ResolveStatePath(resource, resolvedProjectName);
        string authPath = ResolveAuthStatePath(resource);

        if (!File.Exists(statePath))
        {
            return new(statePath, authPath, new BitwardenState());
        }

        try
        {
            await using FileStream stream = File.OpenRead(statePath);
            BitwardenState? state = await JsonSerializer.DeserializeAsync<BitwardenState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            state ??= new BitwardenState();
            state.Normalize();
            return new(statePath, authPath, state);
        }
        catch (Exception ex)
        {
            throw new DistributedApplicationException($"Failed to load Bitwarden state file from '{statePath}'.", ex);
        }
    }

    public async Task SaveAsync(string path, BitwardenState state, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(state);

        state.Normalize();

        string directory = Path.GetDirectoryName(path) ?? throw new DistributedApplicationException($"Unable to determine the Bitwarden state file directory for path '{path}'.");

        try
        {
            Directory.CreateDirectory(directory);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new DistributedApplicationException($"Failed to save Bitwarden state file to '{path}'.", ex);
        }
    }

    private string ResolveStatePath(BitwardenSecretManagerResource resource, string resolvedProjectName)
    {
        IAspireStore aspireStore = services.GetRequiredService<IAspireStore>();

        if (resource.StateFile is { Length: > 0 } stateFile)
        {
            return Path.IsPathRooted(stateFile)
                ? stateFile
                : Path.GetFullPath(Path.Combine(aspireStore.BasePath, stateFile));
        }

        string directory = Path.Combine(aspireStore.BasePath, "bitwarden");
        Directory.CreateDirectory(directory);

        string safeResourceName = string.Concat(resource.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        string identityHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(resource.GetConfiguredProjectIdentityKey(resolvedProjectName))))[..12].ToLowerInvariant();
        string defaultPath = Path.Combine(directory, $"{safeResourceName}.{identityHash}.state.json");

        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        string[] existingPaths = Directory.GetFiles(directory, $"{safeResourceName}.*.state.json", SearchOption.TopDirectoryOnly);
        return existingPaths.Length == 1 ? existingPaths[0] : defaultPath;
    }

    private string ResolveAuthStatePath(BitwardenSecretManagerResource resource)
    {
        IAspireStore aspireStore = services.GetRequiredService<IAspireStore>();

        if (resource.AuthStateFile is { Length: > 0 } authStateFile)
        {
            return Path.IsPathRooted(authStateFile)
                ? authStateFile
                : Path.GetFullPath(Path.Combine(aspireStore.BasePath, authStateFile));
        }

        string safeResourceName = string.Concat(resource.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        return Path.Combine(aspireStore.BasePath, "bitwarden", $"{safeResourceName}.auth.state");
    }
}

internal sealed record BitwardenStateFileContext(string Path, string AuthPath, BitwardenState State);

internal sealed class BitwardenState
{
    public Guid? ProjectId { get; set; }

    public Dictionary<string, Guid> ManagedSecretIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Guid> NameBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        ManagedSecretIds = new Dictionary<string, Guid>(ManagedSecretIds, StringComparer.OrdinalIgnoreCase);
        NameBindings = new Dictionary<string, Guid>(NameBindings, StringComparer.OrdinalIgnoreCase);
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

internal interface IBitwardenSecretManagerProviderFactory
{
    IBitwardenSecretManagerProvider Create(string apiUrl, string identityUrl);
}

internal sealed class BitwardenSecretManagerProviderFactory : IBitwardenSecretManagerProviderFactory
{
    public IBitwardenSecretManagerProvider Create(string apiUrl, string identityUrl)
    {
        return new BitwardenSecretManagerProvider(apiUrl, identityUrl);
    }
}

internal interface IBitwardenSecretManagerProvider : IAsyncDisposable
{
    void Login(string accessToken, string? authStateFile);

    BitwardenProjectInfo? GetProject(Guid projectId);

    BitwardenProjectInfo CreateProject(Guid organizationId, string projectName);

    BitwardenProjectInfo UpdateProject(Guid organizationId, Guid projectId, string projectName);

    BitwardenSecretInfo? GetSecret(Guid secretId);

    IReadOnlyList<BitwardenSecretInfo> GetSecretsByIds(Guid[] secretIds);

    IReadOnlyList<BitwardenSecretIdentifierInfo> ListSecrets(Guid organizationId);

    BitwardenSecretInfo CreateSecret(Guid organizationId, string remoteName, string value, Guid[] projectIds, string note = "");

    BitwardenSecretInfo UpdateSecret(Guid organizationId, Guid secretId, string remoteName, string value, string note, Guid[] projectIds);
}

internal sealed class BitwardenSecretManagerProvider : IBitwardenSecretManagerProvider
{
    private readonly BitwardenClient _client;

    public BitwardenSecretManagerProvider(string apiUrl, string identityUrl)
    {
        _client = new BitwardenClient(new BitwardenSettings
        {
            ApiUrl = apiUrl,
            IdentityUrl = identityUrl
        });
    }

    public void Login(string accessToken, string? authStateFile)
    {
        if (string.IsNullOrWhiteSpace(authStateFile))
        {
            _client.Auth.LoginAccessToken(accessToken);
            return;
        }

        string? directory = Path.GetDirectoryName(authStateFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _client.Auth.LoginAccessToken(accessToken, authStateFile);
    }

    public BitwardenProjectInfo? GetProject(Guid projectId)
    {
        try
        {
            return Map(_client.Projects.Get(projectId));
        }
        catch (BitwardenException)
        {
            return null;
        }
    }

    public BitwardenProjectInfo CreateProject(Guid organizationId, string projectName)
        => Map(_client.Projects.Create(organizationId, projectName));

    public BitwardenProjectInfo UpdateProject(Guid organizationId, Guid projectId, string projectName)
        => Map(_client.Projects.Update(organizationId, projectId, projectName));

    public BitwardenSecretInfo? GetSecret(Guid secretId)
    {
        try
        {
            return Map(_client.Secrets.Get(secretId));
        }
        catch (BitwardenException)
        {
            return null;
        }
    }

    public IReadOnlyList<BitwardenSecretInfo> GetSecretsByIds(Guid[] secretIds)
    {
        if (secretIds.Length == 0)
        {
            return [];
        }

        return _client.Secrets.GetByIds(secretIds).Data.Select(Map).ToArray();
    }

    public IReadOnlyList<BitwardenSecretIdentifierInfo> ListSecrets(Guid organizationId)
    {
        return _client.Secrets.List(organizationId).Data.Select(Map).ToArray();
    }

    public BitwardenSecretInfo CreateSecret(Guid organizationId, string remoteName, string value, Guid[] projectIds, string note = "")
        => Map(_client.Secrets.Create(organizationId, remoteName, value, note, projectIds));

    public BitwardenSecretInfo UpdateSecret(Guid organizationId, Guid secretId, string remoteName, string value, string note, Guid[] projectIds)
        => Map(_client.Secrets.Update(organizationId, secretId, remoteName, value, note, projectIds));

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private static BitwardenProjectInfo Map(ProjectResponse response) => new(response.Id, response.Name, response.OrganizationId);

    private static BitwardenSecretIdentifierInfo Map(SecretIdentifierResponse response) => new(response.Id, response.Key, response.OrganizationId);

    private static BitwardenSecretInfo Map(SecretResponse response) => new(response.Id, response.Key, response.Value, response.Note, response.OrganizationId, response.ProjectId);
}

internal sealed record BitwardenProjectInfo(Guid Id, string Name, Guid OrganizationId);

internal sealed record BitwardenSecretIdentifierInfo(Guid Id, string Key, Guid OrganizationId);

internal sealed record BitwardenSecretInfo(Guid Id, string Key, string Value, string Note, Guid OrganizationId, Guid? ProjectId);