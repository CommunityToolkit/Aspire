#pragma warning disable ASPIREINTERACTION001

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Bitwarden.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

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

        Guid organizationId = await resource.GetResolvedOrganizationIdAsync(cancellationToken).ConfigureAwait(false);
        string accessToken = await resource.GetResolvedManagementAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        string remoteProjectName = await resource.GetResolvedRemoteProjectNameAsync(cancellationToken).ConfigureAwait(false);
        resource.ResolvedRemoteProjectName = remoteProjectName;
        BitwardenStateFileContext stateContext = await stateStore.LoadAsync(resource, remoteProjectName, cancellationToken).ConfigureAwait(false);
        resource.ResolvedStateFile = stateContext.Path;

        await using IBitwardenSecretManagerProvider provider = providerFactory.Create(resource.GetApiUrlOrDefault(), resource.GetIdentityUrlOrDefault());
        provider.Login(accessToken, stateContext.Path);

        IInteractionService? interactionService = services.GetService<IInteractionService>();

        BitwardenProjectInfo project = ReconcileProject(resource, remoteProjectName, stateContext.State, provider, organizationId, logger);
        resource.BindResolvedProjectId(project.Id);

        Dictionary<string, Guid> staleManagedMappings = stateContext.State.ManagedSecretIds
            .Where(entry => resource.ManagedSecrets.All(secret => !string.Equals(secret.LocalName, entry.Key, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        BitwardenLookupContext lookupContext = new(provider, organizationId);

        foreach (BitwardenSecretResource secret in resource.ManagedSecrets)
        {
            await ReconcileManagedSecretAsync(resource, organizationId, secret, stateContext.State, lookupContext, provider, interactionService, logger, cancellationToken, staleManagedMappings).ConfigureAwait(false);
        }

        stateContext.State.ManagedSecretIds = resource.ManagedSecrets
            .Where(secret => secret.SecretId is not null)
            .ToDictionary(secret => secret.LocalName, secret => secret.SecretId!.Value, StringComparer.OrdinalIgnoreCase);

        await ValidateDeclaredSecretReferencesAsync(resource, stateContext.State, lookupContext, interactionService, logger, cancellationToken).ConfigureAwait(false);

        stateContext.State.ProjectId = project.Id;

        await stateStore.SaveAsync(stateContext.Path, stateContext.State, cancellationToken).ConfigureAwait(false);

        return new BitwardenReconciliationResult(project.Id, stateContext.Path);
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
            BitwardenProjectInfo? existingProject = provider.GetProject(existingProjectId);
            if (existingProject is null)
            {
                throw new DistributedApplicationException($"Bitwarden project '{existingProjectId:D}' configured for resource '{resource.Name}' was not found.");
            }

            logger.LogInformation("Using existing Bitwarden project {ProjectId} for resource {ResourceName}.", existingProject.Id, resource.Name);
            return existingProject;
        }

        if (state.ProjectId is Guid persistedProjectId)
        {
            BitwardenProjectInfo? persistedProject = provider.GetProject(persistedProjectId);
            if (persistedProject is not null)
            {
                if (!string.Equals(persistedProject.Name, remoteProjectName, StringComparison.Ordinal))
                {
                    logger.LogInformation(
                        "Updating Bitwarden project {ProjectId} name from {CurrentProjectName} to {DesiredProjectName} for resource {ResourceName}.",
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
                "Persisted Bitwarden project {ProjectId} for resource {ResourceName} was not found. A new project will be created.",
                persistedProjectId,
                resource.Name);
        }

        logger.LogInformation("Creating Bitwarden project {ProjectName} for resource {ResourceName}.", remoteProjectName, resource.Name);
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
        string resolvedValue = await ResolveSecretValueAsync(secretResource.Value, secretResource.LocalName, cancellationToken).ConfigureAwait(false);
        Guid projectId = resource.ProjectId ?? throw new DistributedApplicationException($"Bitwarden resource '{resource.Name}' has not resolved a project identifier.");

        BitwardenSecretInfo secret;
        if (state.ManagedSecretIds.TryGetValue(secretResource.LocalName, out Guid persistedSecretId))
        {
            BitwardenSecretInfo? persistedSecret = lookupContext.GetSecret(persistedSecretId);
            if (persistedSecret is null || persistedSecret.ProjectId != projectId)
            {
                logger.LogWarning(
                    "Managed Bitwarden secret {SecretName} for resource {ResourceName} drifted out of project {ProjectId}. A replacement secret will be created.",
                    secretResource.RemoteName,
                    resource.Name,
                    projectId);

                secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId]);
            }
            else
            {
                secret = EnsureSecretMatches(provider, persistedSecret, projectId, secretResource.RemoteName, resolvedValue);
            }
        }
        else if (secretResource.ExistingSecretId is Guid explicitSecretId)
        {
            BitwardenSecretInfo? explicitSecret = lookupContext.GetSecret(explicitSecretId);
            if (explicitSecret is null)
            {
                throw new DistributedApplicationException($"Bitwarden secret '{explicitSecretId:D}' configured for managed secret '{secretResource.LocalName}' was not found.");
            }

            secret = EnsureSecretMatches(provider, explicitSecret, projectId, secretResource.RemoteName, resolvedValue);
        }
        else
        {
            IReadOnlyList<BitwardenSecretInfo> candidates = lookupContext.FindSecretsByNameInProject(secretResource.RemoteName, projectId);

            if (candidates.Count == 0)
            {
                secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId]);
            }
            else if (candidates.Count == 1)
            {
                if (HasHistoricalManagedMapping(staleManagedMappings, lookupContext, secretResource.RemoteName))
                {
                    logger.LogInformation(
                        "Creating a new Bitwarden secret for managed secret {SecretName} because the previous local identity was renamed and no explicit adoption was configured.",
                        secretResource.LocalName);

                    secret = provider.CreateSecret(organizationId, secretResource.RemoteName, resolvedValue, [projectId]);
                }
                else
                {
                    secret = EnsureSecretMatches(provider, candidates[0], projectId, secretResource.RemoteName, resolvedValue);
                }
            }
            else
            {
                Guid selectedSecretId = await ResolveDuplicateAsync(
                    interactionService,
                    resource,
                    secretResource.RemoteName,
                    candidates,
                    cancellationToken).ConfigureAwait(false);

                BitwardenSecretInfo selectedSecret = candidates.Single(candidate => candidate.Id == selectedSecretId);
                secret = EnsureSecretMatches(provider, selectedSecret, projectId, secretResource.RemoteName, resolvedValue);
            }
        }

        lookupContext.CacheSecret(secret);
        secretResource.SecretId = secret.Id;
        resource.BindResolvedSecret(secret.Id, secretResource.RemoteName, secret.Value);
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
                if (managedSecret.SecretId is Guid managedSecretId)
                {
                    string? managedSecretValue = resource.ResolveSecretValue(managedSecret);
                    if (managedSecretValue is not null)
                    {
                        resource.BindResolvedSecret(managedSecretId, managedSecret.RemoteName, managedSecretValue);
                    }
                }

                continue;
            }

            if (secretReference.SecretId is Guid explicitSecretId)
            {
                BitwardenSecretInfo? secret = lookupContext.GetSecret(explicitSecretId);
                if (secret is null)
                {
                    throw new DistributedApplicationException($"Bitwarden secret '{explicitSecretId:D}' referenced by resource '{resource.Name}' was not found.");
                }

                if (secret.ProjectId != projectId)
                {
                    throw new DistributedApplicationException($"Bitwarden secret '{explicitSecretId:D}' referenced by resource '{resource.Name}' does not belong to Bitwarden project '{projectId:D}'.");
                }

                resource.BindResolvedSecret(secret.Id, secret.Key, secret.Value);
                continue;
            }

            string remoteName = secretReference.RemoteName ?? throw new DistributedApplicationException($"Bitwarden secret reference in resource '{resource.Name}' did not specify a secret name or identifier.");
            BitwardenSecretInfo secretByName;

            if (state.NameBindings.TryGetValue(remoteName, out Guid persistedSecretId))
            {
                BitwardenSecretInfo? persistedSecret = lookupContext.GetSecret(persistedSecretId);
                if (persistedSecret is not null && persistedSecret.ProjectId == projectId)
                {
                    if (!string.Equals(persistedSecret.Key, remoteName, StringComparison.Ordinal))
                    {
                        logger.LogWarning(
                            "Using persisted Bitwarden secret binding {SecretId} for remote name {RemoteName} in resource {ResourceName} even though the remote secret is currently named {CurrentRemoteName}.",
                            persistedSecret.Id,
                            remoteName,
                            resource.Name,
                            persistedSecret.Key);
                    }

                    resource.BindResolvedSecret(persistedSecret.Id, remoteName, persistedSecret.Value);
                    continue;
                }

                logger.LogWarning(
                    "Persisted Bitwarden secret binding {SecretId} for remote name {RemoteName} in resource {ResourceName} is no longer valid. The binding will be re-resolved.",
                    persistedSecretId,
                    remoteName,
                    resource.Name);
            }

            IReadOnlyList<BitwardenSecretInfo> candidates = lookupContext.FindSecretsByNameInProject(remoteName, projectId);
            if (candidates.Count == 0)
            {
                throw new DistributedApplicationException($"Bitwarden secret '{remoteName}' referenced by resource '{resource.Name}' was not found in Bitwarden project '{projectId:D}'.");
            }

            if (candidates.Count == 1)
            {
                secretByName = candidates[0];
            }
            else
            {
                Guid selectedSecretId = await ResolveDuplicateAsync(interactionService, resource, remoteName, candidates, cancellationToken).ConfigureAwait(false);
                secretByName = candidates.Single(candidate => candidate.Id == selectedSecretId);
            }

            state.NameBindings[remoteName] = secretByName.Id;
            resource.BindResolvedSecret(secretByName.Id, remoteName, secretByName.Value);
        }
    }

    private static BitwardenSecretInfo EnsureSecretMatches(
        IBitwardenSecretManagerProvider provider,
        BitwardenSecretInfo secret,
        Guid managedProjectId,
        string remoteName,
        string value)
    {
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

    private static async Task<string> ResolveSecretValueAsync(object valueSource, string secretName, CancellationToken cancellationToken)
    {
        string? value = valueSource switch
        {
            ParameterResource parameter => await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false),
            ReferenceExpression referenceExpression => await referenceExpression.GetValueAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new DistributedApplicationException($"Managed Bitwarden secret '{secretName}' uses unsupported value source type '{valueSource.GetType().Name}'.")
        };

        if (value is null)
        {
            throw new DistributedApplicationException($"Managed Bitwarden secret '{secretName}' did not resolve to a value.");
        }

        return value;
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
        string path = ResolveStatePath(resource, resolvedProjectName);
        if (!File.Exists(path))
        {
            return new(path, new BitwardenState());
        }

        await using FileStream stream = File.OpenRead(path);
        BitwardenState? state = await JsonSerializer.DeserializeAsync<BitwardenState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        state ??= new BitwardenState();
        state.Normalize();
        return new(path, state);
    }

    public async Task SaveAsync(string path, BitwardenState state, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(state);

        state.Normalize();

        string directory = Path.GetDirectoryName(path) ?? throw new DistributedApplicationException($"Unable to determine the Bitwarden state file directory for path '{path}'.");
        Directory.CreateDirectory(directory);

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveStatePath(BitwardenSecretManagerResource resource, string resolvedProjectName)
    {
        if (resource.StateFile is { Length: > 0 } stateFile)
        {
            return stateFile;
        }

        IAspireStore aspireStore = services.GetRequiredService<IAspireStore>();

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
}

internal sealed record BitwardenStateFileContext(string Path, BitwardenState State);

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
    void Login(string accessToken, string stateFile);

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

    public void Login(string accessToken, string stateFile)
    {
        string? directory = Path.GetDirectoryName(stateFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _client.Auth.LoginAccessToken(accessToken, stateFile);
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