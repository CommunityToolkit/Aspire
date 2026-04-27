using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.RustFs;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding RustFs resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RustFsBuilderExtensions
{
    private const string AccessKeyEnvVarName = "RUSTFS_ACCESS_KEY";
    private const string SecretKeyEnvVarName = "RUSTFS_SECRET_KEY";

    /// <summary>
    /// Adds a RustFs container to the application model. The default image is "rustfs/rustfs".
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="accessKey">The parameter used to provide the access key for the RustFs resource. If <see langword="null"/> a random key will be generated.</param>
    /// <param name="secretKey">The parameter used to provide the secret key for the RustFs resource. If <see langword="null"/> a random key will be generated.</param>
    /// <param name="port">The host port for the RustFs S3-compatible API endpoint.</param>
    /// <param name="consolePort">The host port for the RustFs console endpoint.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RustFsResource}"/>.</returns>
    public static IResourceBuilder<RustFsResource> AddRustFs(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? accessKey = null,
        IResourceBuilder<ParameterResource>? secretKey = null,
        int? port = null,
        int? consolePort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var accessKeyParameter = accessKey?.Resource ??
                                 ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder,
                                     $"{name}-accessKey");
        var secretKeyParameter = secretKey?.Resource ??
                                 ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder,
                                     $"{name}-secretKey");

        var resource = new RustFsResource(name, accessKeyParameter, secretKeyParameter);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(RustFsContainerImageTags.Image, RustFsContainerImageTags.Tag)
            .WithImageRegistry(RustFsContainerImageTags.Registry)
            .WithHttpEndpoint(name: RustFsResource.PrimaryEndpointName, port: port,
                targetPort: RustFsResource.PrimaryTargetPort)
            .WithHttpEndpoint(name: RustFsResource.ConsoleEndpointName, port: consolePort,
                targetPort: RustFsResource.ConsoleTargetPort)
            .WithUrlForEndpoint(RustFsResource.PrimaryEndpointName, annot =>
            {
                annot.DisplayText = "Primary";
            })
            .WithUrlForEndpoint(RustFsResource.ConsoleEndpointName, annot =>
            {
                annot.DisplayText = "Console";
            })
            .WithEnvironment("STORAGE_TYPE", "rustfs")
            .WithEnvironment("RUSTFS_ADDRESS", ":" + RustFsResource.PrimaryTargetPort.ToString())
            .WithEnvironment("RUSTFS_CONSOLE_ADDRESS", ":" + RustFsResource.ConsoleTargetPort.ToString())
            .WithEnvironment(AccessKeyEnvVarName, resource.AccessKey)
            .WithEnvironment(SecretKeyEnvVarName, resource.SecretKey)
            .WithHttpHealthCheck("/health", 200, RustFsResource.PrimaryEndpointName);

        return resourceBuilder;
    }

    /// <summary>
    /// Adds a named volume for the data folder to a RustFs container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a RustFs container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var rustfs = builder.AddRustFs("rustfs")
    ///     .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///     .WithReference(rustfs);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<RustFsResource> WithDataVolume(this IResourceBuilder<RustFsResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/data");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a RustFs container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a RustFs container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var rustfs = builder.AddRustFs("rustfs")
    ///     .WithDataBindMount("./data/rustfs/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///     .WithReference(rustfs);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<RustFsResource> WithDataBindMount(this IResourceBuilder<RustFsResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/data");
    }

    /// <summary>
    /// Adds a bucket to the RustFs resource using the MinIO CLI (<c>minio/mc</c>).
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="bucketName">The name of the bucket to create.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ContainerResource}"/> for the bucket creation container.</returns>
    public static IResourceBuilder<ContainerResource> AddBucket(this IResourceBuilder<RustFsResource> builder, string bucketName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new ArgumentException("Bucket name cannot be null or empty.", nameof(bucketName));
        }

        return builder.AddBucket(
            name: $"{builder.Resource.Name}-create-bucket-{bucketName}",
            bucketNames: [bucketName]);
    }

    /// <summary>
    /// Adds multiple buckets to the RustFs resource using the MinIO CLI (<c>minio/mc</c>).
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="bucketNames">The names of the buckets to create.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ContainerResource}"/> for the bucket creation container.</returns>
    public static IResourceBuilder<ContainerResource> AddBucket(this IResourceBuilder<RustFsResource> builder, IReadOnlyList<string> bucketNames)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (bucketNames is null || bucketNames.Count is 0)
        {
            throw new ArgumentException("Bucket names cannot be null or empty.", nameof(bucketNames));
        }

        return builder.AddBucket(
            name: $"{builder.Resource.Name}-create-buckets-{bucketNames[0]}",
            bucketNames: bucketNames);
    }

    private static IResourceBuilder<ContainerResource> AddBucket(
        this IResourceBuilder<RustFsResource> builder,
        [ResourceName] string name,
        IReadOnlyList<string> bucketNames)
    {
        return builder.ApplicationBuilder
            .AddContainer(name, RustFsContainerImageTags.McImage, RustFsContainerImageTags.McTag)
            .WithImageRegistry(RustFsContainerImageTags.McRegistry)
            .WithParentRelationship(builder)
            .WaitFor(builder)
            .WithEntrypoint("/bin/sh")
            .WithArgs(async ctx =>
            {
                var rustFsResource = builder.Resource;

                var accessKey = await rustFsResource.AccessKey.GetValueAsync(ctx.CancellationToken);
                var secretKey = await rustFsResource.SecretKey.GetValueAsync(ctx.CancellationToken);

                var sb = new StringBuilder();

                sb.Append($"mc alias set rustfs {GetRustFsPrimaryUri(rustFsResource)} '{accessKey}' '{secretKey}';");

                foreach (var bucket in bucketNames)
                {
                    if (string.IsNullOrWhiteSpace(bucket))
                    {
                        continue;
                    }

                    sb.Append($"mc mb rustfs/{bucket} --ignore-existing;");
                }

                ctx.Args.Add("-c");
                ctx.Args.Add(sb.ToString());
            });

        static string GetRustFsPrimaryUri(RustFsResource rustFs)
        {
            var endpoint = rustFs.GetEndpoint(RustFsResource.PrimaryEndpointName);
            return $"{endpoint.Scheme}://{rustFs.Name}:{endpoint.TargetPort}";
        }
    }
}
