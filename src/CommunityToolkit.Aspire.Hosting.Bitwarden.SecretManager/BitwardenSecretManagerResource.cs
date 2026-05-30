#pragma warning disable ASPIREATS001

using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Bitwarden Secrets Manager project and secret graph.
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
    private readonly ConfiguredGuidValue _organizationId;
    private readonly ConfiguredStringValue _projectName;

    internal BitwardenSecretManagerResource(
        string name,
        ConfiguredStringValue projectName,
        ConfiguredGuidValue organizationId,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(projectName);
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(managementAccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);

        // Collapse the public overload matrix to one internal representation while
        // still populating the existing exposed properties used elsewhere.
        _projectName = projectName;
        _organizationId = organizationId;
        ConfiguredOrganizationId = organizationId.LiteralValue;
        ConfiguredOrganizationIdParameter = organizationId.Parameter;
        RemoteProjectName = projectName.LiteralValue;
        ConfiguredRemoteProjectNameParameter = projectName.Parameter;
        ManagementAccessToken = managementAccessToken;
        AppHostDirectory = appHostDirectory;
        _projectIdReference = new(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretManagerResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="remoteProjectName">The required remote Bitwarden project name.</param>
    /// <param name="organizationId">The Bitwarden organization identifier.</param>
    /// <param name="managementAccessToken">The access token used to reconcile the Bitwarden project and managed secrets.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    public BitwardenSecretManagerResource(
        string name,
        string remoteProjectName,
        Guid organizationId,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : this(
            name,
            ConfiguredStringValue.FromLiteral(remoteProjectName),
            ConfiguredGuidValue.FromLiteral(organizationId),
            managementAccessToken,
            appHostDirectory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretManagerResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="remoteProjectNameParameter">The parameter that supplies the required remote Bitwarden project name.</param>
    /// <param name="organizationId">The Bitwarden organization identifier.</param>
    /// <param name="managementAccessToken">The access token used to reconcile the Bitwarden project and managed secrets.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    public BitwardenSecretManagerResource(
        string name,
        ParameterResource remoteProjectNameParameter,
        Guid organizationId,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : this(
            name,
            ConfiguredStringValue.FromParameter(remoteProjectNameParameter),
            ConfiguredGuidValue.FromLiteral(organizationId),
            managementAccessToken,
            appHostDirectory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretManagerResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="remoteProjectName">The required remote Bitwarden project name.</param>
    /// <param name="organizationIdParameter">The parameter that supplies the Bitwarden organization identifier.</param>
    /// <param name="managementAccessToken">The access token used to reconcile the Bitwarden project and managed secrets.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    public BitwardenSecretManagerResource(
        string name,
        string remoteProjectName,
        ParameterResource organizationIdParameter,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : this(
            name,
            ConfiguredStringValue.FromLiteral(remoteProjectName),
            ConfiguredGuidValue.FromParameter(organizationIdParameter),
            managementAccessToken,
            appHostDirectory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSecretManagerResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="remoteProjectNameParameter">The parameter that supplies the required remote Bitwarden project name.</param>
    /// <param name="organizationIdParameter">The parameter that supplies the Bitwarden organization identifier.</param>
    /// <param name="managementAccessToken">The access token used to reconcile the Bitwarden project and managed secrets.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    public BitwardenSecretManagerResource(
        string name,
        ParameterResource remoteProjectNameParameter,
        ParameterResource organizationIdParameter,
        ParameterResource managementAccessToken,
        string appHostDirectory)
        : this(
            name,
            ConfiguredStringValue.FromParameter(remoteProjectNameParameter),
            ConfiguredGuidValue.FromParameter(organizationIdParameter),
            managementAccessToken,
            appHostDirectory)
    {
    }

    /// <summary>
    /// Gets the configured remote Bitwarden project name when supplied as a literal value.
    /// </summary>
    public string? RemoteProjectName { get; internal set; }

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
    /// Gets the AppHost auth cache file path override (Bitwarden SDK auth session on the AppHost).
    /// </summary>
    public string? AuthCacheFile { get; internal set; }

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

    internal ParameterResource? ConfiguredRemoteProjectNameParameter { get; set; }

    internal ParameterResource ManagementAccessToken { get; }

    internal string AppHostDirectory { get; }

    internal string? ResolvedRemoteProjectName { get; set; }

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

    internal async Task<Guid> GetResolvedOrganizationIdAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (_organizationId.Parameter is ParameterResource orgIdParam &&
            !orgIdParam.HasValue())
        {
            await orgIdParam.PromptAsync(services, cancellationToken).ConfigureAwait(false);
        }

        return await _organizationId
            .ResolveAsync(Name, "organization", cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<string> GetResolvedManagementAccessTokenAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Messy but no other way to conditionally prompt for the missing token parameter
        if (!ManagementAccessToken.HasValue())
        {
            await ManagementAccessToken.PromptAsync(services, cancellationToken).ConfigureAwait(false);
        }

        string? accessToken = await ManagementAccessToken.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (accessToken is null)
        {
            throw new DistributedApplicationException($"Bitwarden management access token parameter '{ManagementAccessToken.Name}' for resource '{Name}' did not resolve to a value.");
        }

        return accessToken;
    }

    internal async Task<string> GetResolvedRemoteProjectNameAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (_projectName.Parameter is ParameterResource projectNameParam &&
            !projectNameParam.HasValue())
        {
            await projectNameParam.PromptAsync(services, cancellationToken).ConfigureAwait(false);
        }

        return await _projectName
            .ResolveAsync(Name, "project name", cancellationToken)
            .ConfigureAwait(false);
    }

    internal object GetConfiguredOrganizationIdReference() => _organizationId.GetReference(Name, "organization");

    internal object GetConfiguredProjectNameReference() => _projectName.GetReference(Name, "project name");

    internal ReferenceExpression GetApiUrlOrDefault() => ApiUrl;

    internal ReferenceExpression GetIdentityUrlOrDefault() => IdentityUrl;

    internal async ValueTask<string> GetApiUrlAsync(CancellationToken cancellationToken)
        => await ApiUrl.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? DefaultApiUrl;

    internal async ValueTask<string> GetIdentityUrlAsync(CancellationToken cancellationToken)
        => await IdentityUrl.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? DefaultIdentityUrl;

    internal string GetConfiguredProjectIdentityKey(string? resolvedProjectName = null)
        // Existing-project adoption must keep using the remote project ID as the stable key.
        => ExistingProjectId?.ToString("D")
            ?? _projectName.GetIdentityKey(Name, "project name", resolvedProjectName);

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
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__AccessToken"] = ManagementAccessToken;
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__ApiUrl"] = GetApiUrlOrDefault();
        environmentVariables[$"{ConfigurationKeyPrefix}__{connectionName}__IdentityUrl"] = GetIdentityUrlOrDefault();
    }

    internal void ResetResolvedValues()
    {
        ProjectId = null;
        ResolvedRemoteProjectName = null;
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