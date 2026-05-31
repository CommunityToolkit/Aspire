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
    /// <see cref="WithAuthCacheDirectory(string)"/> or <see cref="WithAuthCacheDirectory(IResourceBuilder{ParameterResource})"/> instead.
    /// </remarks>
    /// <param name="volumeName">
    /// The name of the Docker volume. Defaults to
    /// <c>{resourceName}-{connectionName}-bitwarden-auth</c> when <see langword="null"/>.
    /// </param>
    /// <param name="containerDirectory">
    /// The directory inside the container where the volume is mounted.
    /// The auth cache directory is set to <c>{containerDirectory}</c>.
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
                $"Use WithAuthCacheDirectory instead.");
        }

        volumeName ??= $"{_builder.Resource.Name}-{_connectionName}-bitwarden-auth";

        _builder.WithAnnotation(new ContainerMountAnnotation(
            volumeName,
            containerDirectory,
            ContainerMountType.Volume,
            isReadOnly: false));

        _builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{_connectionName}__AuthCacheDirectory",
            containerDirectory);

        return this;
    }

    /// <summary>
    /// Configures the directory where the Bitwarden SDK stores its auth cache inside the resource.
    /// The filename within the directory is managed by the integration.
    /// Use this for process resources or when a fixed container path is known.
    /// </summary>
    /// <param name="appAuthCacheDirectory">The directory path inside the app where the auth cache file is stored.</param>
    /// <returns>This builder.</returns>
    public BitwardenReferenceBuilder<TDestination> WithAuthCacheDirectory(string appAuthCacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appAuthCacheDirectory);

        _builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{_connectionName}__AuthCacheDirectory",
            appAuthCacheDirectory);

        return this;
    }

    /// <summary>
    /// Configures the directory where the Bitwarden SDK stores its auth cache inside the resource,
    /// using a parameter whose value is the directory path.
    /// The filename within the directory is managed by the integration.
    /// Use this when the path must differ between environments or developer machines.
    /// </summary>
    /// <param name="appAuthCacheDirectory">A parameter whose value is the directory path inside the app.</param>
    /// <returns>This builder.</returns>
    public BitwardenReferenceBuilder<TDestination> WithAuthCacheDirectory(IResourceBuilder<ParameterResource> appAuthCacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(appAuthCacheDirectory);

        _builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{_connectionName}__AuthCacheDirectory",
            ReferenceExpression.Create($"{appAuthCacheDirectory.Resource}"));

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
