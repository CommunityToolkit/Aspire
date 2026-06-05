#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREATS001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;
using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Bitwarden Secrets Manager resources.
/// </summary>
public static class BitwardenSecretManagerExtensions
{
    /// <summary>
    /// Adds a Bitwarden Secrets Manager resource. The <paramref name="projectNameOrId"/> parameter
    /// resolves to either a project name (creates or finds by name) or a project identifier GUID
    /// (adopts the existing project by ID).
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="projectNameOrId">The parameter that resolves to the Bitwarden project name or project identifier (GUID).</param>
    /// <param name="organizationId">The parameter that resolves to the Bitwarden organization identifier.</param>
    /// <param name="accessToken">The access token parameter used to manage the Bitwarden project and managed secrets.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<BitwardenSecretManagerResource> AddBitwardenSecretManager(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> projectNameOrId,
        IResourceBuilder<ParameterResource> organizationId,
        IResourceBuilder<ParameterResource> accessToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(projectNameOrId);
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(accessToken);

        return AddBitwardenSecretManagerCore(builder, name, projectNameOrId, organizationId, accessToken);
    }

    /// <summary>
    /// Overrides the Bitwarden API URL.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiUrl">The absolute Bitwarden API URL.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithApiUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string apiUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateAbsoluteUri(apiUrl, nameof(apiUrl));

        builder.Resource.ApiUrl = ReferenceExpression.Create($"{apiUrl}");
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden API URL using a parameter.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="apiUrl">The parameter that resolves to the absolute Bitwarden API URL.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport("withApiUrlFromParameter")]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithApiUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        IResourceBuilder<ParameterResource> apiUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiUrl);

        builder.Resource.ApiUrl = ReferenceExpression.Create($"{apiUrl.Resource}");
        builder.WithReferenceRelationship(apiUrl.Resource);
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden API URL using an external service resource.
    /// The Bitwarden resource will wait for the external service before authenticating.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="server">The external service whose URL is used as the Bitwarden API URL.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport("withApiUrlFromExternalService")]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithApiUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        IResourceBuilder<ExternalServiceResource> server)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(server);

        builder.Resource.ApiUrl = server.Resource.Uri is { } uri
            ? ReferenceExpression.Create($"{uri.AbsoluteUri.TrimEnd('/')}")
            : ReferenceExpression.Create($"{server.Resource.UrlParameter!}");
        builder.WithReferenceRelationship(server.Resource);
        builder.WaitFor(server);
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden API URL using an endpoint from another resource in the Aspire model.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="endpoint">The endpoint reference for the Bitwarden API.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "EndpointReference is not ATS-compatible; polyglot apphosts use the string variant")]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithApiUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        EndpointReference endpoint)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpoint);

        builder.Resource.ApiUrl = ReferenceExpression.Create($"{endpoint}");
        builder.WithReferenceRelationship(endpoint.Resource);
        builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(endpoint.Resource));
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden identity URL.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="identityUrl">The absolute Bitwarden identity URL.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithIdentityUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string identityUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateAbsoluteUri(identityUrl, nameof(identityUrl));

        builder.Resource.IdentityUrl = ReferenceExpression.Create($"{identityUrl}");
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden identity URL using a parameter.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="identityUrl">The parameter that resolves to the absolute Bitwarden identity URL.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport("withIdentityUrlFromParameter")]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithIdentityUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        IResourceBuilder<ParameterResource> identityUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(identityUrl);

        builder.Resource.IdentityUrl = ReferenceExpression.Create($"{identityUrl.Resource}");
        builder.WithReferenceRelationship(identityUrl.Resource);
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden identity URL using an external service resource.
    /// The Bitwarden resource will wait for the external service before authenticating.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="server">The external service whose URL is used as the Bitwarden identity URL.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport("withIdentityUrlFromExternalService")]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithIdentityUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        IResourceBuilder<ExternalServiceResource> server)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(server);

        builder.Resource.IdentityUrl = server.Resource.Uri is { } uri
            ? ReferenceExpression.Create($"{uri.AbsoluteUri.TrimEnd('/')}")
            : ReferenceExpression.Create($"{server.Resource.UrlParameter!}");
        builder.WithReferenceRelationship(server.Resource);
        builder.WaitFor(server);
        return builder;
    }

    /// <summary>
    /// Overrides the Bitwarden identity URL using an endpoint from another resource in the Aspire model.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="endpoint">The endpoint reference for the Bitwarden identity service.</param>
    /// <returns>The resource builder.</returns>
    [AspireExportIgnore(Reason = "EndpointReference is not ATS-compatible; polyglot apphosts use the string variant")]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithIdentityUrl(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        EndpointReference endpoint)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpoint);

        builder.Resource.IdentityUrl = ReferenceExpression.Create($"{endpoint}");
        builder.WithReferenceRelationship(endpoint.Resource);
        builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(endpoint.Resource));
        return builder;
    }

    /// <summary>
    /// Overrides the AppHost cache file path (integration bookkeeping: Bitwarden project ID, secret ID mappings).
    /// Defaults to <c>.bitwarden/{resourceName}.{environment}.json</c> relative to the AppHost directory.
    /// Override to share the cache across multiple AppHost projects, or to store it in a CI cache directory.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="cacheFile">The cache file path, relative to the AppHost directory when not rooted.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithCacheFile(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string cacheFile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheFile);

        builder.Resource.CacheFile = Path.IsPathRooted(cacheFile)
            ? cacheFile
            : Path.GetFullPath(Path.Combine(builder.Resource.AppHostDirectory, cacheFile));

        return builder;
    }

    /// <summary>
    /// Overrides the AppHost auth cache directory (Bitwarden SDK auth session used by the AppHost reconciler).
    /// Defaults to the Aspire store when not set. Override to reuse a cached auth session across CI runs.
    /// To configure the auth cache directory inside the deployed app, use
    /// <see cref="WithBitwardenAuthCacheDirectory{TDestination}(IResourceBuilder{TDestination}, IResourceBuilder{BitwardenSecretManagerResource}, string)"/>.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="authCacheDirectory">The auth cache directory on the AppHost, relative to the Aspire store when not rooted.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<BitwardenSecretManagerResource> WithAuthCacheDirectory(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        string authCacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(authCacheDirectory);

        builder.Resource.AuthCacheDirectory = authCacheDirectory;

        return builder;
    }

    /// <summary>
    /// Gets or creates a Bitwarden secret reference whose Aspire and remote names are the same.
    /// The secret must already exist in Bitwarden; use
    /// <see cref="AddSecret(IResourceBuilder{BitwardenSecretManagerResource}, string)"/> if Aspire should
    /// own and write the secret value.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The Aspire resource name and Bitwarden secret name.</param>
    /// <returns>A resource builder for the secret reference.</returns>
    [AspireExport]
    public static IResourceBuilder<BitwardenSecretResource> GetSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetSecretCore(builder, name, name);
    }

    /// <summary>
    /// Gets or creates a Bitwarden secret reference with distinct Aspire and remote names.
    /// The secret must already exist in Bitwarden; use
    /// <see cref="AddSecret(IResourceBuilder{BitwardenSecretManagerResource}, string, string)"/> if Aspire should
    /// own and write the secret value.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <returns>A resource builder for the secret reference.</returns>
    [AspireExport("getSecretWithRemoteName")]
    public static IResourceBuilder<BitwardenSecretResource> GetSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name,
        string remoteName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        return GetSecretCore(builder, name, remoteName);
    }

    /// <summary>
    /// Gets or creates a Bitwarden secret reference by secret identifier.
    /// Use this when multiple secrets share the same name and the identifier is the only unambiguous key.
    /// The secret must already exist in Bitwarden; use
    /// <see cref="AddSecret(IResourceBuilder{BitwardenSecretManagerResource}, string)"/> if Aspire should
    /// own and write the secret value.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="secretId">The Bitwarden secret identifier.</param>
    /// <returns>A resource builder for the secret reference.</returns>
    [AspireExport("getSecretById")]
    public static IResourceBuilder<BitwardenSecretResource> GetSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name,
        Guid secretId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetSecretCore(builder, name, secretId);
    }

    /// <summary>
    /// Adds a managed Bitwarden secret whose local and remote names are the same.
    /// The secret value is resolved from configuration key <c>Parameters:{parentName}-{name}</c>.
    /// </summary>
    /// <param name="builder">The parent Bitwarden resource builder.</param>
    /// <param name="name">The Aspire resource name and Bitwarden secret name.</param>
    /// <returns>The managed secret resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<BitwardenSecretResource> AddSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return AddSecretCore(builder, name, name);
    }

    /// <summary>
    /// Adds a managed Bitwarden secret with distinct Aspire and remote names.
    /// The secret value is resolved from configuration key <c>Parameters:{parentName}-{name}</c>.
    /// </summary>
    /// <param name="builder">The parent Bitwarden resource builder.</param>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="remoteName">The Bitwarden secret name.</param>
    /// <returns>The managed secret resource builder.</returns>
    [AspireExport("addSecretWithRemoteName")]
    public static IResourceBuilder<BitwardenSecretResource> AddSecret(
        this IResourceBuilder<BitwardenSecretManagerResource> builder,
        [ResourceName] string name,
        string remoteName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        return AddSecretCore(builder, name, remoteName);
    }

    /// <summary>
    /// Injects structured Bitwarden client configuration into the destination resource.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="source">The Bitwarden resource builder.</param>
    /// <param name="connectionName">The logical connection name. Defaults to the Bitwarden resource name.</param>
    /// <returns>The destination resource builder.</returns>
    [AspireExport("withBitwardenSecretManagerReference")]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<BitwardenSecretManagerResource> source,
        string? connectionName = null)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        if (connectionName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        }

        connectionName ??= source.Resource.Name;

        builder.WithReferenceRelationship(source);

        return builder.WithEnvironment(context => source.Resource.ApplyReferenceConfiguration(context.EnvironmentVariables, connectionName));
    }

    /// <summary>
    /// Returns an <see cref="IExpressionValue"/> that resolves to the Bitwarden secret identifier.
    /// Pass it to <c>WithEnvironment</c> to inject the secret ID as an environment variable.
    /// The app then uses the Bitwarden SDK to fetch the secret value at runtime.
    /// </summary>
    /// <param name="secret">The Bitwarden secret resource builder.</param>
    /// <returns>An expression value that resolves to the Bitwarden secret identifier.</returns>
    [AspireExport]
    public static IExpressionValue AsSecretId(this IResourceBuilder<BitwardenSecretResource> secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return new BitwardenSecretIdExpression(secret.Resource);
    }

    /// <summary>
    /// Overrides the Bitwarden access token injected into the connection for <paramref name="source"/>.
    /// By default the management token is used. Supply a least-privilege read-only token here.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="source">The Bitwarden resource whose connection name to target.</param>
    /// <param name="accessToken">The access token parameter for this connection.</param>
    /// <returns>The destination resource builder.</returns>
    [AspireExport("withBitwardenReferenceAccessToken")]
    public static IResourceBuilder<TDestination> WithBitwardenAccessToken<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<BitwardenSecretManagerResource> source,
        IResourceBuilder<ParameterResource> accessToken)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(source);
        return builder.WithBitwardenAccessToken(source.Resource.Name, accessToken);
    }

    /// <summary>
    /// Overrides the Bitwarden access token injected into the specified connection.
    /// By default the management token is used. Supply a least-privilege read-only token here.
    /// Use the source-based overload when the connection name equals the Bitwarden resource name.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="connectionName">The logical connection name, matching the one passed to <see cref="WithReference{TDestination}(IResourceBuilder{TDestination}, IResourceBuilder{BitwardenSecretManagerResource}, string?)"/>.</param>
    /// <param name="accessToken">The access token parameter for this connection.</param>
    /// <returns>The destination resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the source-based overload for the common case; this overload is for the edge case of a custom connection name passed to WithReference")]
    public static IResourceBuilder<TDestination> WithBitwardenAccessToken<TDestination>(
        this IResourceBuilder<TDestination> builder,
        string connectionName,
        IResourceBuilder<ParameterResource> accessToken)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentNullException.ThrowIfNull(accessToken);

        builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{connectionName}__AccessToken",
            accessToken);

        return builder;
    }

    /// <summary>
    /// Configures the directory where the Bitwarden SDK stores its auth cache inside the resource,
    /// for the connection associated with <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="source">The Bitwarden resource whose connection name to target.</param>
    /// <param name="authCacheDirectory">The directory path inside the app where the auth cache file is stored.</param>
    /// <returns>The destination resource builder.</returns>
    [AspireExport("withBitwardenReferenceAuthCacheDirectory")]
    public static IResourceBuilder<TDestination> WithBitwardenAuthCacheDirectory<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<BitwardenSecretManagerResource> source,
        string authCacheDirectory)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(source);
        return builder.WithBitwardenAuthCacheDirectory(source.Resource.Name, authCacheDirectory);
    }

    /// <summary>
    /// Configures the directory where the Bitwarden SDK stores its auth cache inside the resource,
    /// for the specified connection.
    /// Use the source-based overload when the connection name equals the Bitwarden resource name.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="connectionName">The logical connection name.</param>
    /// <param name="authCacheDirectory">The directory path inside the app where the auth cache file is stored.</param>
    /// <returns>The destination resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the source-based overload for the common case; this overload is for the edge case of a custom connection name passed to WithReference")]
    public static IResourceBuilder<TDestination> WithBitwardenAuthCacheDirectory<TDestination>(
        this IResourceBuilder<TDestination> builder,
        string connectionName,
        string authCacheDirectory)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authCacheDirectory);

        builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{connectionName}__AuthCacheDirectory",
            authCacheDirectory);

        return builder;
    }

    /// <summary>
    /// Configures the directory where the Bitwarden SDK stores its auth cache inside the resource,
    /// using a parameter, for the connection associated with <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="source">The Bitwarden resource whose connection name to target.</param>
    /// <param name="authCacheDirectory">A parameter whose value is the directory path inside the app.</param>
    /// <returns>The destination resource builder.</returns>
    [AspireExport("withBitwardenReferenceAuthCacheDirectoryFromParameter")]
    public static IResourceBuilder<TDestination> WithBitwardenAuthCacheDirectory<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<BitwardenSecretManagerResource> source,
        IResourceBuilder<ParameterResource> authCacheDirectory)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(source);
        return builder.WithBitwardenAuthCacheDirectory(source.Resource.Name, authCacheDirectory);
    }

    /// <summary>
    /// Configures the directory where the Bitwarden SDK stores its auth cache inside the resource,
    /// using a parameter, for the specified connection.
    /// Use the source-based overload when the connection name equals the Bitwarden resource name.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="connectionName">The logical connection name.</param>
    /// <param name="authCacheDirectory">A parameter whose value is the directory path inside the app.</param>
    /// <returns>The destination resource builder.</returns>
    [AspireExportIgnore(Reason = "Use the source-based overload for the common case; this overload is for the edge case of a custom connection name passed to WithReference")]
    public static IResourceBuilder<TDestination> WithBitwardenAuthCacheDirectory<TDestination>(
        this IResourceBuilder<TDestination> builder,
        string connectionName,
        IResourceBuilder<ParameterResource> authCacheDirectory)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentNullException.ThrowIfNull(authCacheDirectory);

        builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{connectionName}__AuthCacheDirectory",
            ReferenceExpression.Create($"{authCacheDirectory.Resource}"));

        return builder;
    }

    /// <summary>
    /// Mounts a named volume and configures the Bitwarden SDK to store its auth cache there,
    /// for the connection associated with <paramref name="source"/>. Use this for container resources.
    /// For process resources or when the container path is already known, use
    /// <see cref="WithBitwardenAuthCacheDirectory{TDestination}(IResourceBuilder{TDestination}, IResourceBuilder{BitwardenSecretManagerResource}, string)"/> instead.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="source">The Bitwarden resource whose connection name to target.</param>
    /// <param name="volumeName">The Docker volume name. Defaults to <c>{resourceName}-{connectionName}-bitwarden-auth</c> when <see langword="null"/>.</param>
    /// <param name="containerDirectory">The directory inside the container where the volume is mounted. Defaults to <c>/var/lib/bitwarden</c>.</param>
    /// <returns>The destination resource builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the destination resource is not a container resource.</exception>
    [AspireExport("withBitwardenReferenceAuthCacheVolume")]
    public static IResourceBuilder<TDestination> WithBitwardenAuthCacheVolume<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<BitwardenSecretManagerResource> source,
        string? volumeName = null,
        string containerDirectory = "/var/lib/bitwarden")
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(source);
        return builder.WithBitwardenAuthCacheVolume(source.Resource.Name, volumeName, containerDirectory);
    }

    /// <summary>
    /// Mounts a named volume and configures the Bitwarden SDK to store its auth cache there,
    /// for the specified connection. Use this for container resources.
    /// Use the source-based overload when the connection name equals the Bitwarden resource name.
    /// </summary>
    /// <typeparam name="TDestination">The destination resource type.</typeparam>
    /// <param name="builder">The destination resource builder.</param>
    /// <param name="connectionName">The logical connection name.</param>
    /// <param name="volumeName">The Docker volume name. Defaults to <c>{resourceName}-{connectionName}-bitwarden-auth</c> when <see langword="null"/>.</param>
    /// <param name="containerDirectory">The directory inside the container where the volume is mounted. Defaults to <c>/var/lib/bitwarden</c>.</param>
    /// <returns>The destination resource builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the destination resource is not a container resource.</exception>
    [AspireExportIgnore(Reason = "Use the source-based overload for the common case; this overload is for the edge case of a custom connection name passed to WithReference")]
    public static IResourceBuilder<TDestination> WithBitwardenAuthCacheVolume<TDestination>(
        this IResourceBuilder<TDestination> builder,
        string connectionName,
        string? volumeName = null,
        string containerDirectory = "/var/lib/bitwarden")
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerDirectory);

        if (builder.Resource is not ContainerResource)
        {
            throw new InvalidOperationException(
                $"WithBitwardenAuthCacheVolume requires '{builder.Resource.Name}' to be a container resource. " +
                $"Use WithBitwardenAuthCacheDirectory instead.");
        }

        volumeName ??= $"{builder.Resource.Name}-{connectionName}-bitwarden-auth";

        builder.WithAnnotation(new ContainerMountAnnotation(
            volumeName,
            containerDirectory,
            ContainerMountType.Volume,
            isReadOnly: false));

        builder.WithEnvironment(
            $"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__{connectionName}__AuthCacheDirectory",
            containerDirectory);

        return builder;
    }

    private static IResourceBuilder<BitwardenSecretManagerResource> AddBitwardenSecretManagerCore(
        IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource> projectNameOrId,
        IResourceBuilder<ParameterResource> organizationId,
        IResourceBuilder<ParameterResource> accessToken)
    {
        BitwardenSecretManagerResource resource = new(
            name,
            projectNameOrId.Resource,
            organizationId.Resource,
            accessToken.Resource,
            builder.AppHostDirectory);
        resource.CacheFile = BuildDefaultCachePath(resource, builder.Environment.EnvironmentName);

        var resourceBuilder = ConfigureBitwardenSecretManager(builder.AddResource(resource));

        resourceBuilder.WithReferenceRelationship(accessToken.Resource);
        resourceBuilder.WithReferenceRelationship(projectNameOrId.Resource);
        resourceBuilder.WithReferenceRelationship(organizationId.Resource);

        return resourceBuilder;
    }

    private static IResourceBuilder<BitwardenSecretManagerResource> ConfigureBitwardenSecretManager(
        IResourceBuilder<BitwardenSecretManagerResource> builder)
    {
        bool isPublishMode = builder.ApplicationBuilder.ExecutionContext.IsPublishMode;

        builder.ApplicationBuilder.Services.TryAddSingleton<IBitwardenSecretManagerProviderFactory, BitwardenSecretManagerProviderFactory>();
        builder.ApplicationBuilder.Services.TryAddSingleton<BitwardenSecretManagerProvisioner>();

        var resource = builder.Resource;
        string n = resource.Name;
        string preSyncManagedStepName = $"bitwarden-pre-sync-managed-{n}";
        string authenticateStepName = $"bitwarden-authenticate-{n}";
        string provisionProjectStepName = $"bitwarden-provision-project-{n}";
        string syncManagedSecretsStepName = $"bitwarden-sync-managed-secrets-{n}";
        string provisionSecretsStepName = $"bitwarden-provision-secrets-{n}";
        string patchEnvStepName = $"bitwarden-patch-env-{n}";

        builder.WithPipelineStepFactory(async _ =>
        {
            // Runs before process-parameters (wired in WithPipelineConfiguration below).
            // Only handles managed (AddSecret) secrets — unmanaged (GetSecret) secrets return
            // string.Empty from their valueGetter so ParameterProcessor never adds them to
            // _unresolvedParameters and process-parameters never prompts for them.
            // Prompts for any missing credentials, then fetches existing managed secret values
            // from Bitwarden and writes them to deployment state. Calls IConfigurationRoot.Reload()
            // so _valueGetter reads the fresh values when ParameterProcessor first evaluates them.
            PipelineStep preSyncManagedStep = new()
            {
                Name = preSyncManagedStepName,
                Description = $"Pre-sync Bitwarden managed secret values for '{n}' before parameter prompting",
                Action = async ctx =>
                {
                    var provisioner = ctx.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
                    await provisioner.PreSyncManagedSecretValuesAsync(resource, ctx.Services, ctx.Logger, ctx.CancellationToken).ConfigureAwait(false);
                },
                Resource = resource
            };

            PipelineStep authenticateStep = new()
            {
                Name = authenticateStepName,
                Description = $"Authenticate with Bitwarden Secrets Manager",
                Action = async ctx =>
                {
                    var provisioner = ctx.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
                    await provisioner.AuthenticateAsync(resource, ctx.Services, ctx.Logger, ctx.CancellationToken).ConfigureAwait(false);
                },
                DependsOnSteps = [WellKnownPipelineSteps.DeployPrereq],
                Resource = resource
            };

            PipelineStep provisionProjectStep = new()
            {
                Name = provisionProjectStepName,
                Description = $"Provision Bitwarden project '{n}'",
                Action = async ctx =>
                {
                    var provisioner = ctx.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
                    await provisioner.ProvisionProjectAsync(resource, ctx.Services, ctx.Logger, ctx.CancellationToken).ConfigureAwait(false);
                },
                DependsOnSteps = [authenticateStepName],
                Tags = [WellKnownPipelineTags.ProvisionInfrastructure],
                Resource = resource
            };

            PipelineStep syncManagedSecretsStep = new()
            {
                Name = syncManagedSecretsStepName,
                Description = $"Sync Bitwarden managed secret values for '{n}'",
                Action = async ctx =>
                {
                    var provisioner = ctx.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
                    await provisioner.SyncMissingManagedSecretValuesAsync(resource, ctx.Services, ctx.Logger, ctx.CancellationToken).ConfigureAwait(false);
                },
                DependsOnSteps = [provisionProjectStepName],
                Tags = [WellKnownPipelineTags.ProvisionInfrastructure],
                Resource = resource
            };

            PipelineStep provisionSecretsStep = new()
            {
                Name = provisionSecretsStepName,
                Description = $"Provision Bitwarden secrets for '{n}'",
                Action = async ctx =>
                {
                    var provisioner = ctx.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
                    await provisioner.ProvisionSecretsAsync(resource, ctx.Services, ctx.Logger, ctx.CancellationToken).ConfigureAwait(false);
                },
                DependsOnSteps = [syncManagedSecretsStepName],
                RequiredBySteps = [WellKnownPipelineSteps.Deploy],
                Tags = [WellKnownPipelineTags.ProvisionInfrastructure],
                Resource = resource
            };

            // Workaround: PrepareAsync (Aspire.Hosting.Docker) only resolves ParameterResource and
            // ContainerImageReference sources — custom IValueProvider types are skipped, leaving blank
            // values in .env.{env}. Until PrepareAsync handles IValueProvider generically, this step
            // patches the blanks after prepare-{env} runs. Remove once fixed upstream.
            PipelineStep patchEnvStep = new()
            {
                Name = patchEnvStepName,
                Description = $"Apply Bitwarden-resolved values to environment files for '{n}'",
                Action = async ctx =>
                {
                    await BitwardenSecretManagerDeploymentStep.PatchEnvFilesAsync(ctx, resource).ConfigureAwait(false);
                },
                DependsOnSteps = [provisionSecretsStepName],
                RequiredBySteps = [WellKnownPipelineSteps.Deploy],
                Resource = resource
            };

            return new[] { preSyncManagedStep, authenticateStep, provisionProjectStep, syncManagedSecretsStep, provisionSecretsStep, patchEnvStep };
        });

        builder.WithPipelineConfiguration(context =>
        {
            // Make process-parameters wait for pre-sync so Bitwarden values are in IConfiguration
            // before ParameterProcessor evaluates _valueGetter on managed secrets.
            context.Steps
                .FirstOrDefault(s => s.Name == WellKnownPipelineSteps.ProcessParameters)
                ?.DependsOn(preSyncManagedStepName);

            var patchEnvStep = context.Steps.FirstOrDefault(s => s.Name == patchEnvStepName);
            if (patchEnvStep is null)
            {
                return;
            }

            foreach (var computeEnv in context.Model.Resources.OfType<IComputeEnvironmentResource>())
            {
                string prepareStepName = $"prepare-{computeEnv.Name}";
                string composeUpStepName = $"docker-compose-up-{computeEnv.Name}";

                if (context.Steps.Any(s => s.Name == prepareStepName))
                {
                    patchEnvStep.DependsOn(prepareStepName);
                }

                var composeUpStep = context.Steps.FirstOrDefault(s => s.Name == composeUpStepName);
                composeUpStep?.DependsOn(patchEnvStepName);
            }
        });

        var resourceBuilder = builder.WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = "BitwardenSecretManager",
            State = KnownResourceStates.NotStarted,
            Properties =
            [
                new("CacheFile", builder.Resource.CacheFile)
            ]
        });

        // Only register startup reconciliation in non-publish mode;
        // in publish mode, the publishing step handles reconciliation
        if (!isPublishMode)
        {
            resourceBuilder.OnInitializeResource(async (resource, eventContext, cancellationToken) =>
            {
                await eventContext.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, eventContext.Services), cancellationToken).ConfigureAwait(false);
                await SyncAsync(resource, eventContext.Notifications, eventContext.Services, eventContext.Logger, cancellationToken).ConfigureAwait(false);
            });

            resourceBuilder.WithCommand(
                KnownResourceCommands.RebuildCommand,
                "Reprovision",
                async context =>
                {
                    ResourceNotificationService notifications = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
                    try
                    {
                        await SyncAsync(resource, notifications, context.ServiceProvider, context.Logger, context.CancellationToken).ConfigureAwait(false);
                        return new ExecuteCommandResult { Success = true };
                    }
                    catch (Exception ex)
                    {
                        return new ExecuteCommandResult { Success = false, Message = ex.Message };
                    }
                },
                new CommandOptions
                {
                    IsHighlighted = true,
                    IconName = "ArrowSync",
                    IconVariant = IconVariant.Regular,
                    Description = "Re-run authentication and secret provisioning.",
                    UpdateState = context =>
                    {
                        string? state = context.ResourceSnapshot?.State?.Text;
                        return state == KnownResourceStates.NotStarted
                            ? ResourceCommandState.Disabled
                            : ResourceCommandState.Enabled;
                    }
                });

            resourceBuilder.WithCommand(
                "reset-auth-cache",
                "Reset auth cache",
                async context =>
                {
                    await BitwardenSecretManagerProvisioner.ResetAuthCacheAsync(resource, context.ServiceProvider, context.CancellationToken).ConfigureAwait(false);
                    return new ExecuteCommandResult { Success = true };
                },
                new CommandOptions
                {
                    IconName = "KeyReset",
                    IconVariant = IconVariant.Regular,
                    Description = "Delete the cached Bitwarden authentication session. The next run will perform a fresh login.",
                    UpdateState = context =>
                    {
                        string? state = context.ResourceSnapshot?.State?.Text;
                        bool isActive = state == KnownResourceStates.Waiting || state == KnownResourceStates.Running;
                        return isActive ? ResourceCommandState.Disabled : ResourceCommandState.Enabled;
                    }
                });
        }

        return resourceBuilder;
    }

    private static IResourceBuilder<BitwardenSecretResource> GetSecretCore(
        IResourceBuilder<BitwardenSecretManagerResource> builder,
        string name,
        string remoteName)
    {
        BitwardenSecretResource secret = builder.Resource.GetOrCreateUnmanagedSecret(name, remoteName);

        // If the secret is already in the model (managed or previously registered unmanaged), wrap it.
        IResource? existing = builder.ApplicationBuilder.Resources
            .FirstOrDefault(r => ReferenceEquals(r, secret));
        if (existing is not null)
        {
            return builder.ApplicationBuilder.CreateResourceBuilder(secret);
        }

        builder.WithReferenceRelationship(secret);

        return builder.ApplicationBuilder.AddResource(secret)
            .WithParentRelationship(builder)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Parameter",
                Properties =
                [
                    new(CustomResourceKnownProperties.Source, $"Bitwarden: {remoteName}")
                ],
                State = KnownResourceStates.Waiting
            })
            .ExcludeFromManifest();
    }

    private static IResourceBuilder<BitwardenSecretResource> GetSecretCore(
        IResourceBuilder<BitwardenSecretManagerResource> builder,
        string name,
        Guid secretId)
    {
        BitwardenSecretResource secret = builder.Resource.GetOrCreateUnmanagedSecret(name, secretId);

        IResource? existing = builder.ApplicationBuilder.Resources
            .FirstOrDefault(r => ReferenceEquals(r, secret));
        if (existing is not null)
        {
            return builder.ApplicationBuilder.CreateResourceBuilder(secret);
        }

        builder.WithReferenceRelationship(secret);

        return builder.ApplicationBuilder.AddResource(secret)
            .WithParentRelationship(builder)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Parameter",
                Properties = [],
                State = KnownResourceStates.Waiting
            })
            .ExcludeFromManifest();
    }

    private static IResourceBuilder<BitwardenSecretResource> AddSecretCore(
        IResourceBuilder<BitwardenSecretManagerResource> builder,
        string name,
        string remoteName)
    {
        if (builder.Resource.ManagedSecrets.Any(secret => string.Equals(secret.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DistributedApplicationException($"Bitwarden resource '{builder.Resource.Name}' already declares a managed secret with remote name '{remoteName}'. Managed remote names must be unique per Bitwarden resource.");
        }

        string secretResourceName = $"{builder.Resource.Name}-{name}";
        var config = builder.ApplicationBuilder.Configuration;
        BitwardenSecretResource secret = new(secretResourceName, remoteName, builder.Resource, paramDefault =>
        {
            string key = $"Parameters:{secretResourceName}";
            string? value = config[key];
            return value
                ?? paramDefault?.GetDefaultValue()
                ?? throw new MissingParameterValueException($"Parameter resource could not be used because configuration key '{key}' is missing and the Parameter has no default value.");
        });
        builder.Resource.RegisterSecret(secret);
        builder.WithReferenceRelationship(secret);

        return builder.ApplicationBuilder.AddResource(secret)
            .WithParentRelationship(builder)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Parameter",
                Properties =
                [
                    new(CustomResourceKnownProperties.Source, $"Parameters:{secretResourceName}")
                ],
                State = KnownResourceStates.Waiting
            })
            // Managed secret children are implementation details of the declared graph.
            .ExcludeFromManifest();
    }

    private static ImmutableArray<ResourcePropertySnapshot> MergeProperties(
        ImmutableArray<ResourcePropertySnapshot> existing,
        ResourcePropertySnapshot[]? upsert = null,
        string[]? remove = null)
    {
        Dictionary<string, ResourcePropertySnapshot> dict = existing.ToDictionary(p => p.Name);

        if (remove is not null)
        {
            foreach (string key in remove)
            {
                dict.Remove(key);
            }
        }

        if (upsert is not null)
        {
            foreach (ResourcePropertySnapshot p in upsert)
            {
                dict[p.Name] = p;
            }
        }

        return [.. dict.Values];
    }

    private static async Task SyncAsync(
        BitwardenSecretManagerResource resource,
        ResourceNotificationService notifications,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        DateTime startTime = DateTime.UtcNow;

        // Resolve eagerly so URLs appear in the dashboard from the first state update.
        // For EndpointReference-backed URLs, WaitFor ensures the endpoint is allocated by now.
        string apiUrl = await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false);
        string identityUrl = await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false);
        ImmutableArray<UrlSnapshot> urls =
        [
            new("api", apiUrl, IsInternal: false)
            {
                DisplayProperties = new("API server URL", SortOrder: 0)
            },
            new("identity", identityUrl, IsInternal: false)
            {
                DisplayProperties = new("Identity server URL", SortOrder: 0)
            }
        ];

        await notifications.PublishUpdateAsync(resource, state => state with
        {
            State = KnownResourceStates.Waiting,
            StartTimeStamp = null,
            StopTimeStamp = null,
            ExitCode = null,
            Urls = urls,
            Properties = MergeProperties(state.Properties, remove: ["ProjectId", "Error"])
        }).ConfigureAwait(false);

        BitwardenSecretManagerProvisioner provisioner = services.GetRequiredService<BitwardenSecretManagerProvisioner>();

        try
        {
            // Phase 1: authenticate — waits only for the management access token.
            await provisioner.AuthenticateAsync(resource, services, logger, cancellationToken).ConfigureAwait(false);

            // Phase 2: resolve the project and sync missing managed secret values from upstream.
            await provisioner.ProvisionProjectAsync(resource, services, logger, cancellationToken).ConfigureAwait(false);
            await provisioner.SyncMissingManagedSecretValuesAsync(resource, services, logger, cancellationToken).ConfigureAwait(false);

            // Phase 2.5: pre-populate unmanaged (reference-only) secret values from Bitwarden so that
            // ParameterProcessor does not prompt the user for them before Running state is entered.
            await provisioner.SyncReferenceSecretValuesAsync(resource, services, logger, cancellationToken).ConfigureAwait(false);

            // Phase 3: wait for any remaining parameters before entering Running.
            await WaitForRemainingParametersAsync(resource, services, cancellationToken).ConfigureAwait(false);

            await notifications.PublishUpdateAsync(resource, state => state with
            {
                State = KnownResourceStates.Running,
                StartTimeStamp = startTime,
                Urls = urls,
                Properties = MergeProperties(state.Properties,
                    upsert:
                    [
                        new("CacheFile", resource.CacheFile)
                    ])
            }).ConfigureAwait(false);

            await provisioner.ProvisionSecretsAsync(resource, services, logger, cancellationToken).ConfigureAwait(false);

            await notifications.PublishUpdateAsync(resource, state => state with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.Finished, KnownResourceStateStyles.Success),
                ExitCode = 0,
                StopTimeStamp = DateTime.UtcNow,
                Urls = urls,
                Properties = MergeProperties(state.Properties,
                    upsert:
                    [
                        new("ProjectId", resource.ProjectId!.Value.ToString("D"))
                    ],
                    remove: ["Error"])
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await notifications.PublishUpdateAsync(resource, state => state with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.Exited, KnownResourceStateStyles.Error),
                ExitCode = 1,
                StopTimeStamp = DateTime.UtcNow,
                Urls = urls,
                Properties = MergeProperties(state.Properties,
                    upsert: [new("Error", ex.Message)],
                    remove: ["ProjectId"])
            }).ConfigureAwait(false);

            throw;
        }
    }

    private static async Task WaitForRemainingParametersAsync(
        BitwardenSecretManagerResource resource,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // The access token was already awaited inside AuthenticateAsync.
        // Collect everything else before entering Running state.
        if (resource.ResolvedRemoteProjectName is null && resource.ExistingProjectId is null)
        {
            resource.ResolvedRemoteProjectName = await resource.ResolveProjectIdentityAsync(services, cancellationToken).ConfigureAwait(false);
        }

        await resource.GetResolvedOrganizationIdAsync(services, cancellationToken).ConfigureAwait(false);

        // Each BitwardenSecretResource is a ParameterResource; GetValueAsync waits for
        // ParameterProcessor to resolve the value (from config, user secrets, or interactive prompt).
        foreach (BitwardenSecretResource secret in resource.ManagedSecrets)
        {
            await ((IValueProvider)secret).GetValueAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal static string BuildDefaultCachePath(BitwardenSecretManagerResource resource, string environmentName)
    {
        string safeResourceName = string.Concat(resource.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        string safeEnvironmentName = string.Concat(environmentName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        return Path.Combine(resource.AppHostDirectory, ".bitwarden", $"{safeResourceName}.{safeEnvironmentName}.json");
    }

    private static void ValidateAbsoluteUri(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new ArgumentException("The value must be an absolute URI.", paramName);
        }
    }

}
