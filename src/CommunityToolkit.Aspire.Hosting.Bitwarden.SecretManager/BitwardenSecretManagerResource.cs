#pragma warning disable ASPIREATS001
#pragma warning disable ASPIREINTERACTION001

using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Bitwarden Secrets Manager project and secret graph.
/// </summary>
[AspireExport]
public class BitwardenSecretManagerResource : Resource, IResourceWithWaitSupport
{
    internal const string DefaultApiUrl = "https://api.bitwarden.com";
    internal const string DefaultIdentityUrl = "https://identity.bitwarden.com";
    internal const string ConfigurationKeyPrefix = "Aspire__Bitwarden__SecretManager";

    private readonly BitwardenProjectIdReference _projectIdReference;
    private readonly List<BitwardenSecretResource> _secrets = [];
    private readonly Dictionary<Guid, string> _resolvedSecretValues = [];
    private readonly Dictionary<string, Guid> _resolvedSecretIdsByRemoteName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretManagerResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="projectNameOrIdParameter">The parameter that resolves to the Bitwarden project name or project identifier (GUID).</param>
    /// <param name="organizationIdParameter">The parameter that supplies the Bitwarden organization identifier.</param>
    /// <param name="managementAccessToken">The access token used to reconcile the Bitwarden project and managed secrets.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    public BitwardenSecretManagerResource(
        string name,
        ParameterResource projectNameOrIdParameter,
        ParameterResource organizationIdParameter,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(projectNameOrIdParameter);
        ArgumentNullException.ThrowIfNull(organizationIdParameter);
        ArgumentNullException.ThrowIfNull(managementAccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);

        ConfiguredProjectNameOrIdParameter = projectNameOrIdParameter;
        ConfiguredOrganizationIdParameter = organizationIdParameter;
        ManagementAccessToken = managementAccessToken;
        AppHostDirectory = appHostDirectory;
        _projectIdReference = new(this);
    }

    /// <summary>
    /// Gets the Bitwarden API URL. Defaults to <see cref="DefaultApiUrl"/>.
    /// </summary>
    internal ReferenceExpression ApiUrl { get; set; } = ReferenceExpression.Create($"{DefaultApiUrl}");

    /// <summary>
    /// Gets the Bitwarden identity URL. Defaults to <see cref="DefaultIdentityUrl"/>.
    /// </summary>
    internal ReferenceExpression IdentityUrl { get; set; } = ReferenceExpression.Create($"{DefaultIdentityUrl}");

    /// <summary>
    /// Gets the AppHost cache file path override (integration bookkeeping: project ID, secret ID mappings).
    /// </summary>
    public string? CacheFile { get; internal set; }

    /// <summary>
    /// Gets the AppHost auth cache directory override (Bitwarden SDK auth session on the AppHost).
    /// </summary>
    public string? AuthCacheDirectory { get; internal set; }

    /// <summary>
    /// Gets the existing Bitwarden project identifier when the project is adopted by ID.
    /// Set during <see cref="ResolveProjectIdentityAsync"/> when the configured parameter resolves to a GUID.
    /// </summary>
    public Guid? ExistingProjectId { get; internal set; }

    /// <summary>
    /// Gets the resolved Bitwarden project identifier after initialization.
    /// </summary>
    public Guid? ProjectId { get; internal set; }

    internal ParameterResource ConfiguredOrganizationIdParameter { get; }

    internal ParameterResource ConfiguredProjectNameOrIdParameter { get; }

    internal ParameterResource ManagementAccessToken { get; }

    internal string AppHostDirectory { get; }

    internal string? ResolvedRemoteProjectName { get; set; }

    internal IEnumerable<BitwardenSecretResource> ManagedSecrets => _secrets.Where(s => s.IsManaged);

    internal IEnumerable<BitwardenSecretResource> UnmanagedSecrets => _secrets.Where(s => !s.IsManaged);

    internal IEnumerable<BitwardenSecretResource> DeclaredSecretReferences => _secrets;

    internal BitwardenSecretResource GetOrCreateUnmanagedSecret(string name, string remoteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);

        BitwardenSecretResource? existing =
            _secrets.LastOrDefault(s => string.Equals(s.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        BitwardenSecretResource secret = new($"{Name}-{name}", remoteName, this);
        RegisterSecret(secret);
        return secret;
    }

    internal BitwardenSecretResource GetOrCreateUnmanagedSecret(string name, Guid secretId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        BitwardenSecretResource? existing = _secrets.FirstOrDefault(
            s => !s.IsManaged && s.ExistingSecretId == secretId);
        if (existing is not null)
        {
            return existing;
        }

        BitwardenSecretResource secret = new($"{Name}-{name}", secretId, this);
        RegisterSecret(secret);
        return secret;
    }

    internal async Task<Guid> GetResolvedOrganizationIdAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (!ConfiguredOrganizationIdParameter.HasValue())
        {
            ThrowIfNonInteractive(services, ConfiguredOrganizationIdParameter.Name);
            await ConfiguredOrganizationIdParameter.PromptAsync(services, cancellationToken).ConfigureAwait(false);
        }

        string? value = await ConfiguredOrganizationIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (!Guid.TryParse(value, out Guid organizationId))
        {
            throw new DistributedApplicationException(
                $"Bitwarden organization parameter '{ConfiguredOrganizationIdParameter.Name}' for resource '{Name}' did not resolve to a valid GUID.");
        }

        return organizationId;
    }

    internal async Task<string> GetResolvedManagementAccessTokenAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!ManagementAccessToken.HasValue())
        {
            ThrowIfNonInteractive(services, ManagementAccessToken.Name);
            await ManagementAccessToken.PromptAsync(services, cancellationToken).ConfigureAwait(false);
        }

        string? accessToken = await ManagementAccessToken.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (accessToken is null)
        {
            throw new DistributedApplicationException($"Bitwarden management access token parameter '{ManagementAccessToken.Name}' for resource '{Name}' did not resolve to a value.");
        }

        return accessToken;
    }

    /// <summary>
    /// Resolves the project identity parameter. When the resolved value is a GUID, sets
    /// <see cref="ExistingProjectId"/> and returns <see langword="null"/> (ID-based adoption).
    /// Otherwise returns the project name string and caches it in <see cref="ResolvedRemoteProjectName"/>.
    /// </summary>
    internal async Task<string?> ResolveProjectIdentityAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (!ConfiguredProjectNameOrIdParameter.HasValue())
        {
            ThrowIfNonInteractive(services, ConfiguredProjectNameOrIdParameter.Name);
            await ConfiguredProjectNameOrIdParameter.PromptAsync(services, cancellationToken).ConfigureAwait(false);
        }

        string? value = await ConfiguredProjectNameOrIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DistributedApplicationException(
                $"Bitwarden project name or ID parameter '{ConfiguredProjectNameOrIdParameter.Name}' for resource '{Name}' did not resolve to a value.");
        }

        if (Guid.TryParse(value, out Guid projectId))
        {
            ExistingProjectId = projectId;
            return null;
        }

        ResolvedRemoteProjectName = value;
        return value;
    }

    internal object GetConfiguredOrganizationIdReference() => ConfiguredOrganizationIdParameter;

    internal object GetConfiguredProjectNameOrIdReference() => ConfiguredProjectNameOrIdParameter;

    internal ReferenceExpression GetApiUrlOrDefault() => ApiUrl;

    internal ReferenceExpression GetIdentityUrlOrDefault() => IdentityUrl;

    internal async ValueTask<string> GetApiUrlAsync(CancellationToken cancellationToken)
        => await ApiUrl.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? DefaultApiUrl;

    internal async ValueTask<string> GetIdentityUrlAsync(CancellationToken cancellationToken)
        => await IdentityUrl.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? DefaultIdentityUrl;

    internal string GetProjectNameDisplayValue()
        => ResolvedRemoteProjectName ?? ConfiguredProjectNameOrIdParameter.Name;

    internal string? ResolveSecretValue(BitwardenSecretResource secret)
    {
        Guid? secretId = secret.ResolvedSecretId;
        if (secretId is Guid explicitSecretId && _resolvedSecretValues.TryGetValue(explicitSecretId, out string? explicitValue))
        {
            return explicitValue;
        }

        if (secret.RemoteName is string remoteName &&
            _resolvedSecretIdsByRemoteName.TryGetValue(remoteName, out Guid resolvedSecretId) &&
            _resolvedSecretValues.TryGetValue(resolvedSecretId, out string? remoteNameValue))
        {
            return remoteNameValue;
        }

        return null;
    }

    internal void ApplyReferenceConfiguration(IDictionary<string, object> environmentVariables, string connectionName)
    {
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__OrganizationId"] = GetConfiguredOrganizationIdReference();
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__ProjectId"] = _projectIdReference;
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__AccessToken"] = ManagementAccessToken;
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__ApiUrl"] = GetApiUrlOrDefault();
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__IdentityUrl"] = GetIdentityUrlOrDefault();
    }

    private void ThrowIfNonInteractive(IServiceProvider services, string parameterName)
    {
        var interactionService = services.GetService<IInteractionService>();
        if (interactionService is null || !interactionService.IsAvailable)
        {
            throw new DistributedApplicationException(
                $"Parameter '{parameterName}' for Bitwarden resource '{Name}' has no value and cannot be prompted in non-interactive mode. " +
                $"Provide it with '--parameter {parameterName}=<value>', or run 'aspire deploy' interactively to configure the deployment first.");
        }
    }

    internal void ResetResolvedValues()
    {
        ProjectId = null;
        ExistingProjectId = null;
        ResolvedRemoteProjectName = null;
        _resolvedSecretValues.Clear();
        _resolvedSecretIdsByRemoteName.Clear();

        foreach (BitwardenSecretResource secret in _secrets)
        {
            secret.SecretId = null;
        }
    }

    internal void BindResolvedProjectId(Guid projectId)
    {
        ProjectId = projectId;
    }

    internal void BindResolvedSecret(Guid secretId, string remoteName, string value)
    {
        _resolvedSecretValues[secretId] = value;
        _resolvedSecretIdsByRemoteName[remoteName] = secretId;
    }

    internal void RegisterSecret(BitwardenSecretResource secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (!_secrets.Contains(secret))
        {
            _secrets.Add(secret);
        }
    }

    internal BitwardenSecretResource? FindManagedSecretByRemoteName(string remoteName)
    {
        return _secrets.LastOrDefault(s => s.IsManaged && string.Equals(s.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase));
    }
}
