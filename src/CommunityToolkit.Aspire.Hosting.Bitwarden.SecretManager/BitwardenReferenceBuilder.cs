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
    /// Mounts a named volume into the resource and configures the Bitwarden SDK to store its auth
    /// cache file there. Use this for container resources to persist the auth session across restarts.
    /// </summary>
    /// <remarks>
    /// Requires the destination resource to be a container resource.
    /// For process resources or when the container path is already known, use
    /// <see cref="WithAuthCacheFile(string)"/> or <see cref="WithAuthCacheFile(IResourceBuilder{ParameterResource})"/> instead.
    /// </remarks>
    /// <param name="volumeName">
    /// The name of the Docker volume. Defaults to
    /// <c>{resourceName}-{connectionName}-bitwarden-auth</c> when <see langword="null"/>.
    /// </param>
    /// <param name="containerDirectory">
    /// The directory inside the container where the volume is mounted.
    /// The auth cache file is placed at <c>{containerDirectory}/auth.json</c>.
    /// Defaults to <c>/var/lib/bitwarden</c>.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the destination resource is not a container resource.
    /// </exception>
    public BitwardenReferenceBuilder<TDestination> WithAuthCacheVolume(
        string? volumeName = null,
        string containerDirectory = "/var/lib/bitwarden")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerDirectory);

        if (_builder.Resource is not ContainerResource)
        {
            throw new InvalidOperationException(
                $"WithAuthCacheVolume requires '{_builder.Resource.Name}' to be a container resource. " +
                $"Use WithAuthCacheFile instead.");
        }

        volumeName ??= $"{_builder.Resource.Name}-{_connectionName}-bitwarden-auth";

        _builder.WithAnnotation(new ContainerMountAnnotation(
            volumeName,
            containerDirectory,
            ContainerMountType.Volume,
            isReadOnly: false));

        _builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{_connectionName}__AuthCacheFile",
            $"{containerDirectory}/auth.json");

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
    /// <param name="secret">The Bitwarden secret resource.</param>
    /// <returns>This builder.</returns>
    public BitwardenReferenceBuilder<TDestination> WithBitwardenSecretId(
        string environmentVariableName,
        BitwardenSecretResource secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariableName);
        ArgumentNullException.ThrowIfNull(secret);

        BitwardenSecretManagerExtensions.AttachSecretDependencies(_builder, secret);
        _builder.WithEnvironment(environmentVariableName, new BitwardenSecretIdExpression(secret));
        return this;
    }
}
