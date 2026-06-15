using System.Data.Common;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.RustFs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

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
    [AspireExport]
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
            .WithEnvironment(AccessKeyEnvVarName, $"{resource.AccessKey}")
            .WithEnvironment(SecretKeyEnvVarName, $"{resource.SecretKey}")
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
    [AspireExport]
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
    [AspireExport]
    public static IResourceBuilder<RustFsResource> WithDataBindMount(this IResourceBuilder<RustFsResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/data");
    }

    /// <summary>
    /// Sets the AWS signing region used when creating buckets via the S3 API.
    /// Defaults to <c>us-east-1</c> if not specified.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="region">The AWS signing region (e.g. <c>us-east-1</c>, <c>ap-northeast-1</c>).</param>
    /// <returns>The <see cref="IResourceBuilder{RustFsResource}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<RustFsResource> WithSigningRegion(this IResourceBuilder<RustFsResource> builder, string region)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        builder.Resource.SigningRegion = region;
        return builder;
    }

    /// <summary>
    /// Adds a bucket to the RustFs resource. The bucket is created by issuing a signed
    /// <c>PUT</c> request to the RustFs S3 API after the server resource becomes ready.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="bucketName">The name of the bucket to create.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for the bucket resource.</returns>
    [AspireExport]
    public static IResourceBuilder<RustFsBucketResource> AddBucket(this IResourceBuilder<RustFsResource> builder, string bucketName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);

        return builder.AddBucketCore(
            name: $"{builder.Resource.Name}-{SanitizeForResourceName(bucketName)}",
            bucketName: bucketName);
    }

    /// <summary>
    /// Adds multiple buckets to the RustFs resource. Each bucket is registered as its own
    /// <see cref="RustFsBucketResource"/> child resource and created via the RustFs S3 API
    /// once the server resource becomes ready.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="bucketNames">The names of the buckets to create.</param>
    /// <returns>The original <see cref="IResourceBuilder{RustFsResource}"/> for further chaining.</returns>
    [AspireExport("addBuckets")]
    public static IResourceBuilder<RustFsResource> AddBucket(this IResourceBuilder<RustFsResource> builder, IReadOnlyList<string> bucketNames)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (bucketNames is null || bucketNames.Count is 0)
        {
            throw new ArgumentException("Bucket names cannot be null or empty.", nameof(bucketNames));
        }

        foreach (var bucketName in bucketNames)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                continue;
            }

            builder.AddBucket(bucketName);
        }

        return builder;
    }

    private static IResourceBuilder<RustFsBucketResource> AddBucketCore(
        this IResourceBuilder<RustFsResource> builder,
        [ResourceName] string name,
        string bucketName)
    {
        var parent = builder.Resource;
        var bucketResource = new RustFsBucketResource(name, bucketName, parent);

        var bucketBuilder = builder.ApplicationBuilder
            .AddResource(bucketResource)
            .WithParentRelationship(builder)
            .WithInitialState(new()
            {
                ResourceType = "RustFsBucket",
                State = new ResourceStateSnapshot("Waiting", KnownResourceStateStyles.Info),
                Properties = [new(CustomResourceKnownProperties.Source, bucketName)],
            });

        builder.ApplicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(parent, (@event, cancellationToken) =>
        {
            _ = Task.Run(() => CreateBucketOnReadyAsync(@event, bucketResource, parent, cancellationToken), cancellationToken);
            return Task.CompletedTask;
        });

        return bucketBuilder;
    }

    private static async Task CreateBucketOnReadyAsync(
        ResourceReadyEvent @event,
        RustFsBucketResource bucketResource,
        RustFsResource parent,
        CancellationToken cancellationToken)
    {
        var notificationService = @event.Services.GetRequiredService<ResourceNotificationService>();
        var logger = @event.Services.GetRequiredService<ResourceLoggerService>().GetLogger(bucketResource);

        try
        {
            await notificationService.PublishUpdateAsync(bucketResource, state => state with
            {
                State = new ResourceStateSnapshot("Creating", KnownResourceStateStyles.Info),
            }).ConfigureAwait(false);

            var accessKey = await parent.AccessKey.GetValueAsync(cancellationToken).ConfigureAwait(false);
            var secretKey = await parent.SecretKey.GetValueAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("RustFs access key or secret key is not available.");
            }

            var connectionString = await parent.ConnectionStringExpression.GetValueAsync(cancellationToken).ConfigureAwait(false);
            var endpointUri = ParseEndpointUri(connectionString);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
            };

            await CreateBucketAsync(httpClient, endpointUri, bucketResource.BucketName, accessKey, secretKey, parent.SigningRegion, logger, cancellationToken).ConfigureAwait(false);

            await notificationService.PublishUpdateAsync(bucketResource, state => state with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success),
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create RustFs bucket '{Bucket}'", bucketResource.BucketName);
            await notificationService.PublishUpdateAsync(bucketResource, state => state with
            {
                State = new ResourceStateSnapshot(ex.Message, KnownResourceStateStyles.Error),
            }).ConfigureAwait(false);
        }
    }

    private static Uri ParseEndpointUri(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("RustFs connection string is not available.");
        }

        var csb = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (!csb.TryGetValue("Endpoint", out var endpointObj) ||
            endpointObj is not string endpoint ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("RustFs connection string is missing a valid 'Endpoint' value.");
        }

        return uri;
    }

    private static async Task CreateBucketAsync(
        HttpClient httpClient,
        Uri endpointUri,
        string bucketName,
        string accessKey,
        string secretKey,
        string signingRegion,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var hostHeader = endpointUri.IsDefaultPort
            ? endpointUri.Host
            : $"{endpointUri.Host}:{endpointUri.Port}";

        var headers = RustFsS3Signer.SignPutBucket(
            hostHeader: hostHeader,
            bucketName: bucketName,
            accessKey: accessKey,
            secretKey: secretKey,
            region: signingRegion,
            timestamp: DateTimeOffset.UtcNow);

        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(endpointUri, Uri.EscapeDataString(bucketName)));
        foreach (var (key, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Created RustFs bucket '{Bucket}'", bucketName);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict ||
            body.Contains("BucketAlreadyOwnedByYou", StringComparison.Ordinal) ||
            body.Contains("BucketAlreadyExists", StringComparison.Ordinal))
        {
            logger.LogInformation("RustFs bucket '{Bucket}' already exists", bucketName);
            return;
        }

        throw new InvalidOperationException(
            $"Failed to create RustFs bucket '{bucketName}': HTTP {(int)response.StatusCode} {response.ReasonPhrase} — {body}");
    }

    private static string SanitizeForResourceName(string name) =>
        new(name.Select(static c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray());
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental
