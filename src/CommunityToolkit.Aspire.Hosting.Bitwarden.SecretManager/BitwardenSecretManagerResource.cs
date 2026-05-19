#pragma warning disable ASPIREATS001

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Bitwarden Secrets Manager project resource.
/// </summary>
[AspireExport(ExposeProperties = true)]
public class BitwardenSecretManagerResource : Resource, IResourceWithWaitSupport
{
    internal const string DefaultApiUrl = "https://api.bitwarden.com";
    internal const string DefaultIdentityUrl = "https://identity.bitwarden.com";
    internal const string ConfigurationKeyPrefix = "Aspire__Bitwarden__SecretManager";

    private readonly BitwardenProjectIdReference _projectIdReference;
    private readonly List<BitwardenSecretResource> _managedSecrets = [];
    private readonly List<IBitwardenSecretReference> _declaredSecretReferences = [];
    private readonly Dictionary<Guid, string> _resolvedSecretValues = [];
    private readonly Dictionary<string, Guid> _resolvedSecretIdsByRemoteName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretManagerResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="organizationId">The Bitwarden organization identifier.</param>
    /// <param name="managementAccessToken">The access token used to reconcile the Bitwarden project and managed secrets.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    public BitwardenSecretManagerResource(
        string name,
        Guid organizationId,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(managementAccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);

        ConfiguredOrganizationId = organizationId;
        ManagementAccessToken = managementAccessToken;
        AppHostDirectory = appHostDirectory;
        RemoteProjectName = name;
        _projectIdReference = new(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretManagerResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="organizationIdParameter">The parameter that supplies the Bitwarden organization identifier.</param>
    /// <param name="managementAccessToken">The access token used to reconcile the Bitwarden project and managed secrets.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    public BitwardenSecretManagerResource(
        string name,
        ParameterResource organizationIdParameter,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(organizationIdParameter);
        ArgumentNullException.ThrowIfNull(managementAccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);

        ConfiguredOrganizationIdParameter = organizationIdParameter;
        ManagementAccessToken = managementAccessToken;
        AppHostDirectory = appHostDirectory;
        RemoteProjectName = name;
        _projectIdReference = new(this);
    }

    /// <summary>
    /// Gets the remote Bitwarden project name that this resource reconciles.
    /// </summary>
    public string RemoteProjectName { get; internal set; }

    /// <summary>
    /// Gets the Bitwarden API URL override.
    /// </summary>
    public string? ApiUrl { get; internal set; }

    /// <summary>
    /// Gets the Bitwarden identity URL override.
    /// </summary>
    public string? IdentityUrl { get; internal set; }

    /// <summary>
    /// Gets the explicit state file path used for the Bitwarden SDK login state.
    /// </summary>
    public string? StateFile { get; internal set; }

    /// <summary>
    /// Gets the existing Bitwarden project identifier to adopt.
    /// </summary>
    public Guid? ExistingProjectId { get; internal set; }

    /// <summary>
    /// Gets the resolved Bitwarden project identifier after initialization.
    /// </summary>
    public Guid? ProjectId { get; internal set; }

    internal Guid? ConfiguredOrganizationId { get; }

    internal ParameterResource? ConfiguredOrganizationIdParameter { get; }

    internal ParameterResource ManagementAccessToken { get; }

    internal ParameterResource? RuntimeAccessToken { get; set; }

    internal string AppHostDirectory { get; }

    internal string? ResolvedStateFile { get; set; }

    internal IReadOnlyList<BitwardenSecretResource> ManagedSecrets => _managedSecrets;

    internal IReadOnlyList<IBitwardenSecretReference> DeclaredSecretReferences => _declaredSecretReferences;

    internal IBitwardenSecretReference GetSecretReference(string remoteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);

        BitwardenSecretResource? managedSecret = FindManagedSecretByRemoteName(remoteName);
        if (managedSecret is not null)
        {
            return managedSecret;
        }

        BitwardenSecretReference secretReference = new(remoteName, null, this);
        RegisterSecretReference(secretReference);
        return secretReference;
    }

    internal IBitwardenSecretReference GetSecretReference(Guid secretId)
    {
        BitwardenSecretReference secretReference = new(null, secretId, this);
        RegisterSecretReference(secretReference);
        return secretReference;
    }

    /// <summary>
    /// Gets a Bitwarden secret reference by remote name.
    /// </summary>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <returns>A Bitwarden secret reference.</returns>
    public IBitwardenSecretReference GetSecret(string remoteName) => GetSecretReference(remoteName);

    /// <summary>
    /// Gets a Bitwarden secret reference by secret identifier.
    /// </summary>
    /// <param name="secretId">The Bitwarden secret identifier.</param>
    /// <returns>A Bitwarden secret reference.</returns>
    public IBitwardenSecretReference GetSecret(Guid secretId) => GetSecretReference(secretId);

    internal async Task<Guid> GetResolvedOrganizationIdAsync(CancellationToken cancellationToken)
    {
        if (ConfiguredOrganizationId is Guid organizationId)
        {
            return organizationId;
        }

        string? organizationIdValue = await ConfiguredOrganizationIdParameter!.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (!Guid.TryParse(organizationIdValue, out organizationId))
        {
            throw new DistributedApplicationException($"Bitwarden organization parameter '{ConfiguredOrganizationIdParameter.Name}' for resource '{Name}' did not resolve to a valid GUID.");
        }

        return organizationId;
    }

    internal async Task<string> GetResolvedManagementAccessTokenAsync(CancellationToken cancellationToken)
    {
        string? accessToken = await ManagementAccessToken.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (accessToken is null)
        {
            throw new DistributedApplicationException($"Bitwarden management access token parameter '{ManagementAccessToken.Name}' for resource '{Name}' did not resolve to a value.");
        }

        return accessToken;
    }

    internal object GetConfiguredOrganizationIdReference()
    {
        if (ConfiguredOrganizationIdParameter is not null)
        {
            return ConfiguredOrganizationIdParameter;
        }

        if (ConfiguredOrganizationId is Guid organizationId)
        {
            return organizationId.ToString("D");
        }

        throw new DistributedApplicationException($"Bitwarden resource '{Name}' does not have an organization identifier configured.");
    }

    internal object GetEffectiveAccessTokenReference() => RuntimeAccessToken ?? ManagementAccessToken;

    internal string GetApiUrlOrDefault() => ApiUrl ?? DefaultApiUrl;

    internal string GetIdentityUrlOrDefault() => IdentityUrl ?? DefaultIdentityUrl;

    internal string GetConfiguredProjectIdentityKey() => ExistingProjectId?.ToString("D") ?? RemoteProjectName;

    internal string? ResolveSecretValue(IBitwardenSecretReference secretReference)
    {
        Guid? secretId = secretReference.SecretId;
        if (secretId is Guid explicitSecretId && _resolvedSecretValues.TryGetValue(explicitSecretId, out string? explicitValue))
        {
            return explicitValue;
        }

        if (secretReference.RemoteName is string remoteName &&
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
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__AccessToken"] = GetEffectiveAccessTokenReference();
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__ApiUrl"] = GetApiUrlOrDefault();
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__IdentityUrl"] = GetIdentityUrlOrDefault();
    }

    internal void ResetResolvedValues()
    {
        ProjectId = null;
        _resolvedSecretValues.Clear();
        _resolvedSecretIdsByRemoteName.Clear();

        foreach (BitwardenSecretResource secret in _managedSecrets)
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

    internal void RegisterManagedSecret(BitwardenSecretResource secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        _managedSecrets.Add(secret);
        RegisterSecretReference(secret);
    }

    internal void RegisterSecretReference(IBitwardenSecretReference secretReference)
    {
        ArgumentNullException.ThrowIfNull(secretReference);

        if (!_declaredSecretReferences.Contains(secretReference))
        {
            _declaredSecretReferences.Add(secretReference);
        }
    }

    internal BitwardenSecretResource? FindManagedSecretByRemoteName(string remoteName)
    {
        return _managedSecrets.LastOrDefault(secret => string.Equals(secret.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class BitwardenProjectIdReference(BitwardenSecretManagerResource resource) : IManifestExpressionProvider, IValueProvider, IValueWithReferences
{
    public string ValueExpression => $"{{{resource.Name}.projectId}}";

    IEnumerable<object> IValueWithReferences.References => [resource];

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(resource.ProjectId?.ToString("D"));
    }
}