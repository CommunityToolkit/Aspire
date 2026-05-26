using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Configures the Bitwarden connection for a dependent resource.
/// Obtained from a <see cref="BitwardenSecretManagerExtensions.WithReference{TDestination}(IResourceBuilder{TDestination}, IResourceBuilder{BitwardenSecretManagerResource}, System.Action{BitwardenReferenceBuilder{TDestination}}, string?)"/> call.
/// </summary>
/// <typeparam name="TDestination">The dependent resource type.</typeparam>
public sealed class BitwardenReferenceBuilder<TDestination>
    where TDestination : IResourceWithEnvironment
{
    private readonly IResourceBuilder<TDestination> _builder;
    private readonly string _connectionName;

    internal BitwardenReferenceBuilder(
        IResourceBuilder<TDestination> builder,
        string connectionName)
    {
        _builder = builder;
        _connectionName = connectionName;
    }

    /// <summary>
    /// Overrides the access token injected into this client.
    /// By default the management token is used. Supply a least-privilege read-only token here.
    /// </summary>
    /// <remarks>
    /// The token must be granted read permissions to the Bitwarden project manually in the
    /// Bitwarden web vault or CLI — Bitwarden does not expose an API for this.
    /// For a newly created project, do this after the first AppHost run that creates the project.
    /// </remarks>
    /// <param name="accessToken">The access token parameter for this client.</param>
    /// <returns>This builder.</returns>
    public BitwardenReferenceBuilder<TDestination> WithAccessToken(IResourceBuilder<ParameterResource> accessToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        _builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{_connectionName}__AccessToken",
            accessToken);

        return this;
    }

    /// <summary>
    /// Injects the Bitwarden SDK auth cache file path into the resource using a fixed path.
    /// </summary>
    /// <param name="appAuthCacheFile">The auth cache file path inside the app.</param>
    /// <returns>This builder.</returns>
    public BitwardenReferenceBuilder<TDestination> WithAuthCacheFile(string appAuthCacheFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appAuthCacheFile);

        _builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{_connectionName}__AuthCacheFile",
            appAuthCacheFile);

        return this;
    }

    /// <summary>
    /// Injects the Bitwarden SDK auth cache file path into the resource using a parameter.
    /// </summary>
    /// <param name="appAuthCacheFile">A parameter whose value is the auth cache file path inside the app.</param>
    /// <returns>This builder.</returns>
    public BitwardenReferenceBuilder<TDestination> WithAuthCacheFile(IResourceBuilder<ParameterResource> appAuthCacheFile)
    {
        ArgumentNullException.ThrowIfNull(appAuthCacheFile);

        _builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{_connectionName}__AuthCacheFile",
            appAuthCacheFile);

        return this;
    }

    /// <summary>
    /// Injects a Bitwarden secret identifier into a destination environment variable.
    /// The app uses the Bitwarden SDK to fetch the value by ID at runtime.
    /// </summary>
    /// <param name="environmentVariableName">The destination environment variable name.</param>
    /// <param name="secretReference">The Bitwarden secret reference.</param>
    /// <returns>This builder.</returns>
    public BitwardenReferenceBuilder<TDestination> WithBitwardenSecretId(
        string environmentVariableName,
        IBitwardenSecretReference secretReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariableName);
        ArgumentNullException.ThrowIfNull(secretReference);

        BitwardenSecretManagerExtensions.AttachSecretDependencies(_builder, secretReference);
        _builder.WithEnvironment(environmentVariableName, new BitwardenSecretIdExpression(secretReference));
        return this;
    }
}
