#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES002

using System.Collections.Immutable;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Bitwarden.Sdk;
using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;
using Microsoft.Extensions.Configuration;
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

        ParameterResourceExtensions.SetCompatibilityLogger(logger);
        resource.ResetResolvedValues();
        logger.LogDebug("Starting Bitwarden authentication for resource '{ResourceName}'.", resource.Name);

        try
        {
            // Bitwarden does not throw a descriptive exception when TLS validation fails.
            // Proactively check the TLS trust environment before attempting to authenticate with Bitwarden.
            await BitwardenTlsValidator.ValidateTlsCertDirAsync(resource, logger, cancellationToken).ConfigureAwait(false);

            // Auth cache path resolution internally waits for the management access token,
            // making the token the only input required before authentication can proceed.
            // Project name and other parameters are collected in a separate phase after auth.
            string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
            BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Loaded Bitwarden AppHost cache from '{AppHostCachePath}'.", cacheContext.CachePath);

            string accessToken = await resource.GetResolvedManagementAccessTokenAsync(services, cancellationToken).ConfigureAwait(false);

            string apiUrl = await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false);
            string identityUrl = await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Creating Bitwarden provider with API URL '{ApiUrl}' and Identity URL '{IdentityUrl}'.", apiUrl, identityUrl);
            await using IBitwardenSecretManagerProvider provider = providerFactory.Create(apiUrl, identityUrl);

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
                ?? await resource.GetResolvedRemoteProjectNameAsync(services, cancellationToken).ConfigureAwait(false);

            string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
            BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);

            Guid organizationId = await resource.GetResolvedOrganizationIdAsync(services, cancellationToken).ConfigureAwait(false);
            string accessToken = await resource.GetResolvedManagementAccessTokenAsync(services, cancellationToken).ConfigureAwait(false);

            await using IBitwardenSecretManagerProvider provider = providerFactory.Create(
                await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false),
                await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false));
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
    /// Fetches values for all unmanaged (reference-only) secret resources from Bitwarden and binds them
    /// so that <see cref="ParameterProcessor"/> does not prompt the user for them.
    /// Requires <see cref="ProvisionProjectAsync"/> to have completed successfully first.
    /// </summary>
    public async Task SyncReferenceSecretValuesAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        if (!resource.UnmanagedSecrets.Any())
        {
            return;
        }

        logger.LogDebug("Starting reference secret value sync for resource '{ResourceName}'.", resource.Name);

        Guid projectId = resource.ProjectId ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' has not resolved a project identifier.");
        Guid organizationId = await resource.GetResolvedOrganizationIdAsync(services, cancellationToken).ConfigureAwait(false);
        string accessToken = await resource.GetResolvedManagementAccessTokenAsync(services, cancellationToken).ConfigureAwait(false);
        string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
        BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);

        await using IBitwardenSecretManagerProvider provider = providerFactory.Create(
            await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false),
            await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false));
        provider.Login(accessToken, cacheContext.AuthCachePath);

        BitwardenLookupContext lookupContext = new(provider, organizationId);

        foreach (BitwardenSecretResource secret in resource.UnmanagedSecrets)
        {
            BitwardenSecretInfo secretInfo;

            Guid? secretId = secret.ExistingSecretId ?? secret.SecretId;
            if (secretId is Guid id)
            {
                logger.LogDebug("Looking up reference secret '{SecretName}' by ID {SecretId}.", secret.LocalName, id);
                BitwardenSecretInfo? found = lookupContext.GetSecret(id);
                if (found is null)
                {
                    throw new DistributedApplicationException($"Bitwarden secret '{id:D}' referenced by resource '{resource.Name}' was not found.");
                }

                if (found.ProjectId != projectId)
                {
                    throw new DistributedApplicationException($"Bitwarden secret '{id:D}' referenced by resource '{resource.Name}' does not belong to Bitwarden project '{projectId:D}'.");
                }

                secretInfo = found;
            }
            else
            {
                logger.LogDebug("Looking up reference secret '{SecretName}' by name '{RemoteName}' in project {ProjectId}.", secret.LocalName, secret.RemoteName, projectId);
                IReadOnlyList<BitwardenSecretInfo> candidates = lookupContext.FindSecretsByNameInProject(secret.RemoteName, projectId);
                if (candidates.Count == 0)
                {
                    throw new DistributedApplicationException($"Bitwarden secret '{secret.RemoteName}' referenced by resource '{resource.Name}' was not found in Bitwarden project '{projectId:D}'.");
                }

                if (candidates.Count > 1)
                {
                    throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' resolved {candidates.Count} secrets named '{secret.RemoteName}' in project '{projectId:D}'. Resolve the duplicate in Bitwarden or reference by secret ID instead.");
                }

                secretInfo = candidates[0];
            }

            secret.SecretId = secretInfo.Id;
            resource.BindResolvedSecret(secretInfo.Id, secretInfo.Key, secretInfo.Value);
            secret.ResolveWaitForValue(secretInfo.Value);
            await NotifySecretValueResolvedAsync(secret, secretInfo.Value, services, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Synced reference secret '{SecretName}' from Bitwarden secret {SecretId}.", secret.LocalName, secretInfo.Id);
        }

        logger.LogInformation("Synced {Count} reference secret values from Bitwarden for resource '{ResourceName}'.", resource.UnmanagedSecrets.Count(), resource.Name);
    }

    /// <summary>
    /// Binds existing Bitwarden values for managed secrets whose local parameter values are missing.
    /// Requires <see cref="ProvisionProjectAsync"/> to have completed successfully first.
    /// </summary>
    public async Task SyncMissingManagedSecretValuesAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogDebug("Starting upstream managed secret value sync for resource '{ResourceName}'.", resource.Name);

        Guid projectId = resource.ProjectId ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' has not resolved a project identifier.");
        Guid organizationId = await resource.GetResolvedOrganizationIdAsync(services, cancellationToken).ConfigureAwait(false);
        string accessToken = await resource.GetResolvedManagementAccessTokenAsync(services, cancellationToken).ConfigureAwait(false);
        string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
        BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);
        IInteractionService? interactionService = services.GetService<IInteractionService>();

        await using IBitwardenSecretManagerProvider provider = providerFactory.Create(
            await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false),
            await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false));
        provider.Login(accessToken, cacheContext.AuthCachePath);

        BitwardenLookupContext lookupContext = new(provider, organizationId);
        int syncedCount = 0;

        foreach (BitwardenSecretResource secret in resource.ManagedSecrets)
        {
            if (secret.HasValue())
            {
                logger.LogDebug("Skipping upstream sync for managed secret '{SecretName}' because a local value is already configured.", secret.LocalName);
                continue;
            }

            BitwardenSecretInfo? upstreamSecret = await ResolveExistingManagedSecretAsync(
                resource,
                projectId,
                secret,
                cacheContext.Cache,
                lookupContext,
                interactionService,
                logger,
                cancellationToken).ConfigureAwait(false);

            if (upstreamSecret is null)
            {
                logger.LogDebug("No upstream value found for managed secret '{SecretName}'. A local parameter value is still required.", secret.LocalName);
                continue;
            }

            secret.SecretId = upstreamSecret.Id;
            resource.BindResolvedSecret(upstreamSecret.Id, secret.RemoteName, upstreamSecret.Value);
            secret.ResolveWaitForValue(upstreamSecret.Value);
            await NotifySecretValueResolvedAsync(secret, upstreamSecret.Value, services, cancellationToken).ConfigureAwait(false);
            syncedCount++;
            logger.LogInformation("Synced managed secret '{SecretName}' from existing Bitwarden secret {SecretId}.", secret.LocalName, upstreamSecret.Id);
        }

        logger.LogInformation("Synced {SyncedSecretCount} managed secret values from upstream for resource '{ResourceName}'.", syncedCount, resource.Name);
    }

    /// <summary>
    /// Prompts for any missing credentials, then fetches existing managed secret values from Bitwarden
    /// and writes everything to the deployment state so that <c>process-parameters</c> finds the values
    /// in <see cref="IConfiguration"/> and does not re-prompt the user. Runs before <c>process-parameters</c>.
    /// Skips the Bitwarden fetch (but not the credential prompts) when the project ID is not yet cached.
    /// </summary>
    /// <remarks>
    /// Why IConfiguration and not TCS: <c>ParameterProcessor.InitializeParametersAsync</c> unconditionally
    /// creates a fresh <c>WaitForValueTcs</c> then immediately calls <c>ValueInternal</c>, which reads the
    /// lazy-evaluated <c>_valueGetter</c>. That getter reads <see cref="IConfiguration"/>. Writing the value
    /// to the deployment state file and calling <see cref="IConfigurationRoot.Reload"/> before
    /// <c>process-parameters</c> runs is the only way to pre-populate the value without racing against
    /// TCS creation.
    /// </remarks>
    public async Task PreSyncManagedSecretValuesAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        if (!resource.ManagedSecrets.Any())
        {
            return;
        }

        IConfiguration configuration = services.GetRequiredService<IConfiguration>();
        IDeploymentStateManager deploymentStateManager = services.GetRequiredService<IDeploymentStateManager>();
        int savedCount = 0;

        // Resolve the access token via IConfiguration first (avoids HasValue() which would evaluate
        // _lazyValue and potentially poison it). Prompt via ParameterProcessor if absent, then
        // persist to deployment state so process-parameters finds it there and does not prompt again.
        string accessTokenConfigKey = $"Parameters:{resource.ManagementAccessToken.Name}";
        string? accessToken = configuration[accessTokenConfigKey];
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogDebug("Access token not in configuration; prompting before pre-sync for '{ResourceName}'.", resource.Name);
            resource.ManagementAccessToken.InitializeWaitForValue();
            await resource.ManagementAccessToken.PromptAsync(services, cancellationToken).ConfigureAwait(false);
            accessToken = resource.ManagementAccessToken.GetResolvedWaitForValue();
            if (string.IsNullOrEmpty(accessToken))
            {
                logger.LogDebug("Access token prompt dismissed; skipping pre-sync for '{ResourceName}'.", resource.Name);
                return;
            }
            var tokenSlot = await deploymentStateManager.AcquireSectionAsync(accessTokenConfigKey, cancellationToken).ConfigureAwait(false);
            tokenSlot.SetValue(accessToken);
            await deploymentStateManager.SaveSectionAsync(tokenSlot, cancellationToken).ConfigureAwait(false);
            savedCount++;
        }

        // Resolve the organization ID the same way — literal or via IConfiguration; prompt if needed.
        Guid organizationId;
        if (resource.ConfiguredOrganizationId is Guid literalOrgId)
        {
            organizationId = literalOrgId;
        }
        else if (resource.ConfiguredOrganizationIdParameter is ParameterResource orgIdParam)
        {
            string orgIdConfigKey = $"Parameters:{orgIdParam.Name}";
            string? orgIdString = configuration[orgIdConfigKey];
            if (string.IsNullOrEmpty(orgIdString))
            {
                logger.LogDebug("Organization ID not in configuration; prompting before pre-sync for '{ResourceName}'.", resource.Name);
                orgIdParam.InitializeWaitForValue();
                await orgIdParam.PromptAsync(services, cancellationToken).ConfigureAwait(false);
                orgIdString = orgIdParam.GetResolvedWaitForValue();
                if (string.IsNullOrEmpty(orgIdString))
                {
                    logger.LogDebug("Organization ID prompt dismissed; skipping pre-sync for '{ResourceName}'.", resource.Name);
                    return;
                }
                var orgIdSlot = await deploymentStateManager.AcquireSectionAsync(orgIdConfigKey, cancellationToken).ConfigureAwait(false);
                orgIdSlot.SetValue(orgIdString);
                await deploymentStateManager.SaveSectionAsync(orgIdSlot, cancellationToken).ConfigureAwait(false);
                savedCount++;
            }
            if (!Guid.TryParse(orgIdString, out organizationId))
            {
                logger.LogDebug("Organization ID '{Value}' is not a valid GUID; skipping pre-sync for '{ResourceName}'.", orgIdString, resource.Name);
                return;
            }
        }
        else
        {
            return;
        }

        string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
        BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);

        // Need a project ID from a previous run's cache or an explicitly configured one.
        Guid? projectId = cacheContext.Cache.ProjectId ?? resource.ExistingProjectId;
        if (projectId is null)
        {
            logger.LogDebug("Project ID not available from cache or explicit configuration; skipping managed secret pre-sync for '{ResourceName}'.", resource.Name);
            // Still reload for any credentials written above so process-parameters skips them.
            if (savedCount > 0 && configuration is IConfigurationRoot earlyRoot)
                earlyRoot.Reload();
            return;
        }

        logger.LogDebug("Pre-syncing managed secret values for resource '{ResourceName}' from project {ProjectId}.", resource.Name, projectId);

        await using IBitwardenSecretManagerProvider provider = providerFactory.Create(
            await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false),
            await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false));
        provider.Login(accessToken, cacheContext.AuthCachePath);

        BitwardenLookupContext lookupContext = new(provider, organizationId);
        int preResolvedCount = 0;

        foreach (BitwardenSecretResource secret in resource.ManagedSecrets)
        {
            // ConfigurationKey is internal to Aspire.Hosting; replicate it — managed secrets are never connection strings.
            string configKey = $"Parameters:{secret.Name}";

            // Check IConfiguration directly — never call HasValue() or ValueInternal here.
            // Lazy<string> caches exceptions under ExecutionAndPublication (the default), so evaluating
            // _lazyValue before process-parameters runs (which would find no value yet) permanently
            // poisons it. Subsequent calls by ParameterProcessor would re-throw the cached exception
            // and ignore the reloaded IConfiguration value.
            if (!string.IsNullOrWhiteSpace(configuration[configKey]))
            {
                logger.LogDebug("Skipping pre-sync for managed secret '{SecretName}': value already in configuration.", secret.LocalName);
                continue;
            }

            BitwardenSecretInfo? existing = await ResolveExistingManagedSecretAsync(
                resource,
                projectId.Value,
                secret,
                cacheContext.Cache,
                lookupContext,
                interactionService: null, // never prompt during pre-sync
                logger,
                cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                logger.LogDebug("No upstream value found for managed secret '{SecretName}' during pre-sync.", secret.LocalName);
                continue;
            }

            var slot = await deploymentStateManager.AcquireSectionAsync(configKey, cancellationToken).ConfigureAwait(false);
            slot.SetValue(existing.Value);
            await deploymentStateManager.SaveSectionAsync(slot, cancellationToken).ConfigureAwait(false);
            preResolvedCount++;

            logger.LogInformation("Pre-resolved managed secret '{SecretName}' from Bitwarden secret {SecretId}.", secret.LocalName, existing.Id);
        }

        savedCount += preResolvedCount;
        if (savedCount > 0)
        {
            // Force IConfiguration to re-read the updated deployment state file.
            // AddJsonFile is registered with reloadOnChange:false so manual Reload() is required.
            // _lazyValue in ParameterResource has not been evaluated yet at this point (process-parameters
            // hasn't run), so the next call to _valueGetter will pick up the fresh values.
            if (configuration is IConfigurationRoot configRoot)
            {
                configRoot.Reload();
            }

            logger.LogInformation("Pre-synced {Count} managed secret values from Bitwarden for resource '{ResourceName}'; IConfiguration reloaded.", preResolvedCount, resource.Name);
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
                ?? await resource.GetResolvedRemoteProjectNameAsync(services, cancellationToken).ConfigureAwait(false);

            string authCachePath = await ResolveAuthCachePathAsync(resource, services, cancellationToken).ConfigureAwait(false);
            BitwardenCacheContext cacheContext = await BitwardenStore.LoadAsync(resource, authCachePath, cancellationToken).ConfigureAwait(false);

            Guid organizationId = await resource.GetResolvedOrganizationIdAsync(services, cancellationToken).ConfigureAwait(false);
            string accessToken = await resource.GetResolvedManagementAccessTokenAsync(services, cancellationToken).ConfigureAwait(false);

            IInteractionService? interactionService = services.GetService<IInteractionService>();

            await using IBitwardenSecretManagerProvider provider = providerFactory.Create(
                await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false),
                await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false));
            provider.Login(accessToken, cacheContext.AuthCachePath);

            Dictionary<string, Guid> staleManagedMappings = cacheContext.Cache.ManagedSecretIds
                .Where(entry => resource.ManagedSecrets.All(secret => !string.Equals(secret.LocalName, entry.Key, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

            if (staleManagedMappings.Count > 0)
            {
                logger.LogInformation("Found {StaleSecretCount} stale managed secret mappings that will be cleaned up.", staleManagedMappings.Count);
            }

            BitwardenLookupContext lookupContext = new(provider, organizationId);

            logger.LogInformation("Provisioning {ManagedSecretCount} managed secrets for resource '{ResourceName}'.", resource.ManagedSecrets.Count(), resource.Name);
            foreach (BitwardenSecretResource secret in resource.ManagedSecrets)
            {
                logger.LogDebug("Processing managed secret '{SecretName}' (remote name: {RemoteName}).", secret.LocalName, secret.RemoteName);
                await ReconcileManagedSecretAsync(resource, organizationId, secret, cacheContext.Cache, lookupContext, provider, interactionService, logger, staleManagedMappings, services, cancellationToken).ConfigureAwait(false);
            }

            cacheContext.Cache.ManagedSecretIds = resource.ManagedSecrets
                .Where(secret => secret.SecretId is not null)
                .ToDictionary(secret => secret.LocalName, secret => secret.SecretId!.Value, StringComparer.OrdinalIgnoreCase);

            logger.LogInformation("Validating {DeclaredSecretCount} declared secret references for resource '{ResourceName}'.", resource.DeclaredSecretReferences.Count(), resource.Name);
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
        IReadOnlyDictionary<string, Guid> staleManagedMappings,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Resolving value for managed secret '{SecretName}'.", secretResource.LocalName);
        string resolvedValue = await ((IValueProvider)secretResource).GetValueAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new DistributedApplicationException($"Managed Bitwarden secret '{secretResource.LocalName}' did not resolve to a value.");
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

                secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId], SecretUpdateAudit.CreationNote());
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
                secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId], SecretUpdateAudit.CreationNote());
                logger.LogInformation("Created new secret {SecretId} for managed secret '{SecretName}'.", secret.Id, secretResource.LocalName);
            }
            else if (candidates.Count == 1)
            {
                if (HasHistoricalManagedMapping(staleManagedMappings, lookupContext, secretResource.RemoteName))
                {
                    logger.LogWarning(
                        "Creating a new Bitwarden secret for managed secret '{SecretName}' because the previous local identity was renamed and no explicit adoption was configured.",
                        secretResource.LocalName);

                    secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId], SecretUpdateAudit.CreationNote());
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
        secretResource.ResolveWaitForValue(secret.Value);
        await NotifySecretValueResolvedAsync(secretResource, secret.Value, services, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Successfully provisioned managed secret '{SecretName}' with ID {SecretId}.", secretResource.LocalName, secret.Id);
    }

    private static async Task<BitwardenSecretInfo?> ResolveExistingManagedSecretAsync(
        BitwardenSecretManagerResource resource,
        Guid projectId,
        BitwardenSecretResource secretResource,
        BitwardenCache state,
        BitwardenLookupContext lookupContext,
        IInteractionService? interactionService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (secretResource.ExistingSecretId is Guid explicitSecretId)
        {
            logger.LogDebug("Checking explicitly configured upstream secret {SecretId} for managed secret '{SecretName}'.", explicitSecretId, secretResource.LocalName);
            BitwardenSecretInfo? explicitSecret = lookupContext.GetSecret(explicitSecretId);
            if (explicitSecret is not null)
            {
                return explicitSecret;
            }

            return null;
        }

        if (state.ManagedSecretIds.TryGetValue(secretResource.LocalName, out Guid persistedSecretId))
        {
            logger.LogDebug("Checking persisted upstream secret {SecretId} for managed secret '{SecretName}'.", persistedSecretId, secretResource.LocalName);
            BitwardenSecretInfo? persistedSecret = lookupContext.GetSecret(persistedSecretId);
            if (persistedSecret is not null && persistedSecret.ProjectId == projectId)
            {
                return persistedSecret;
            }
        }

        logger.LogDebug("Searching upstream for managed secret '{SecretName}' with remote name '{RemoteName}' in project {ProjectId}.", secretResource.LocalName, secretResource.RemoteName, projectId);
        IReadOnlyList<BitwardenSecretInfo> candidates = lookupContext.FindSecretsByNameInProject(secretResource.RemoteName, projectId);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        Guid selectedSecretId = await ResolveDuplicateAsync(
            interactionService,
            resource,
            secretResource.RemoteName,
            candidates,
            cancellationToken).ConfigureAwait(false);

        return candidates.Single(candidate => candidate.Id == selectedSecretId);
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

        foreach (BitwardenSecretResource secretReference in resource.DeclaredSecretReferences)
        {
            if (secretReference.IsManaged)
            {
                logger.LogDebug("Processing declared reference to managed secret '{SecretName}'.", secretReference.LocalName);
                if (secretReference.SecretId is Guid managedSecretId)
                {
                    string? managedSecretValue = resource.ResolveSecretValue(secretReference);
                    if (managedSecretValue is not null)
                    {
                        resource.BindResolvedSecret(managedSecretId, secretReference.RemoteName, managedSecretValue);
                        logger.LogDebug("Bound declared reference to managed secret {SecretId} for '{SecretName}'.", managedSecretId, secretReference.LocalName);
                    }
                }

                continue;
            }

            // Unmanaged (reference-only) BitwardenSecretResource falls through to the ID or name lookup below.
            if (secretReference.ResolvedSecretId is Guid explicitSecretId)
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

                    secretReference.SecretId = persistedSecret.Id;
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

            secretReference.SecretId = secretByName.Id;
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
        var audit = SecretUpdateAudit.Compare(secret, managedProjectId, remoteName, value);
        if (!audit.RequiresUpdate)
        {
            return secret;
        }

        Guid[] projectIds = BuildProjectIds(secret.ProjectId, managedProjectId);
        return provider.UpdateSecret(secret.OrganizationId, secret.Id, remoteName, value, audit.PrependTo(secret.Note), projectIds);
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

    // Called after the Bitwarden provisioner has resolved a managed secret's value so the dashboard
    // parameter state reflects the resolved value instead of staying in "ValueMissing".
    private static async Task NotifySecretValueResolvedAsync(
        BitwardenSecretResource secretResource,
        string resolvedValue,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
#pragma warning disable ASPIREINTERACTION001
        ParameterProcessor? paramProcessor = services.GetService<ParameterProcessor>();
        if (paramProcessor is not null)
        {
            ParameterResourceExtensions.MarkParameterResolved(paramProcessor, secretResource);
        }
#pragma warning restore ASPIREINTERACTION001

        ResourceNotificationService? notificationService = services.GetService<ResourceNotificationService>();
        if (notificationService is null)
        {
            return;
        }

        await notificationService.PublishUpdateAsync(secretResource, s =>
        {
            // Update the "Value" property (IsSensitive because secrets are always masked in the dashboard).
            const string valuePropName = "Value";
            var props = s.Properties;
            var valueProp = new ResourcePropertySnapshot(valuePropName, resolvedValue) { IsSensitive = secretResource.Secret };
            int idx = -1;
            for (int i = 0; i < props.Length; i++)
            {
                if (string.Equals(props[i].Name, valuePropName, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            props = idx >= 0 ? props.SetItem(idx, valueProp) : [.. props, valueProp];

            return s with { State = KnownResourceStates.Running, Properties = props };
        }).ConfigureAwait(false);
    }

    private static async Task<string> ResolveAuthCachePathAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // The filename is always the token UUID — only the directory can be overridden.
        string? accessToken = await resource.GetResolvedManagementAccessTokenAsync(services, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new DistributedApplicationException($"Bitwarden access token for resource '{resource.Name}' did not resolve to a value.");
        }

        string tokenId = ParseTokenId(resource.Name, accessToken);

        if (resource.AuthCacheDirectory is { Length: > 0 } authCacheDirectory)
        {
            string resolvedDirectory = Path.IsPathRooted(authCacheDirectory)
                ? authCacheDirectory
                : Path.GetFullPath(Path.Combine(services.GetRequiredService<IAspireStore>().BasePath, authCacheDirectory));
            return Path.Combine(resolvedDirectory, tokenId);
        }

        IAspireStore store = services.GetRequiredService<IAspireStore>();
        return Path.Combine(store.BasePath, ".bitwarden", tokenId);
    }

    // Access token format: 0.<uuid>.<secret>:<base64_key>
    // Returns the UUID component (e.g. "ec2c1d46-6a4b-4751-a310-af9601317f2d").
    private static string ParseTokenId(string resourceName, string accessToken)
    {
        ReadOnlySpan<char> span = accessToken.AsSpan();
        int firstDot = span.IndexOf('.');
        if (firstDot >= 0)
        {
            ReadOnlySpan<char> rest = span[(firstDot + 1)..];
            int secondDot = rest.IndexOf('.');
            if (secondDot >= 0 && Guid.TryParse(rest[..secondDot], out Guid tokenId))
            {
                return tokenId.ToString("D");
            }
        }

        throw new DistributedApplicationException(
            $"Bitwarden access token for resource '{resourceName}' does not match the expected format '0.<uuid>.<secret>:<base64_key>'.");
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

internal sealed record SecretUpdateAudit(
    bool ValueChanged, string PreviousValue,
    bool NameChanged, string PreviousName,
    bool ProjectChanged, Guid? PreviousProjectId)
{
    public bool RequiresUpdate => ValueChanged || NameChanged || ProjectChanged;

    public static SecretUpdateAudit Compare(BitwardenSecretInfo secret, Guid managedProjectId, string remoteName, string value)
    {
        return new(
                ValueChanged: !string.Equals(secret.Value, value, StringComparison.Ordinal),
                PreviousValue: secret.Value,
                NameChanged: !string.Equals(secret.Key, remoteName, StringComparison.Ordinal),
                PreviousName: secret.Key,
                ProjectChanged: secret.ProjectId != managedProjectId,
                PreviousProjectId: secret.ProjectId);
    }

    public static string CreationNote()
    {
        return $"[{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}] Created";
    }

    public string PrependTo(string existingNote)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var changes = new List<string>();
        if (NameChanged)
        {
            changes.Add($"key renamed (previous: {PreviousName})");
        }

        if (ProjectChanged)
        {
            changes.Add($"project changed (previous: {PreviousProjectId})");
        }

        if (ValueChanged)
        {
            changes.Add($"value changed (previous: {PreviousValue})");
        }

        string entry = $"[{timestamp}] {string.Join(", ", changes)}";
        return string.IsNullOrEmpty(existingNote) ? entry : $"{entry}\n{existingNote}";
    }
}
