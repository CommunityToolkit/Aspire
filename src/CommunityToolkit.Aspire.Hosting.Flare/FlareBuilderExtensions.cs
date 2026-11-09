// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Flare resources to the application model.
/// </summary>
public static class FlareBuilderExtensions
{
    private const int OtlpGrpcPort = 4317;
    private const int OtlpHttpPort = 4318;
    private const int ApiPort = 8080;
    private const int DashboardPort = 3000;

    // Flare's images look the database up under this connection name and create their schema in a
    // database of this name, so neither can be configured.
    private const string ClickHouseConnectionName = "clickhousedb";
    private const string ClickHouseDatabaseName = "clickhousedb";
    private const string RedisConnectionName = "redis";

    private const string OtlpHeadersVariable = "OTEL_EXPORTER_OTLP_HEADERS";
    private const string DashboardApiKeyHeader = "x-otlp-api-key";

    private const string IdentityMountPath = "/data/identity";
    private const string IdentityDbPath = "/data/identity/flare-identity.db";

    /// <summary>
    /// Adds a Flare observability stack to the application model: an OTLP ingest service (the returned
    /// resource), the query API, the alert worker and the dashboard. Containers are used for local development.
    /// The default images are <inheritdoc cref="FlareContainerImageTags.IngestImage"/>,
    /// <inheritdoc cref="FlareContainerImageTags.ApiImage"/>, <inheritdoc cref="FlareContainerImageTags.AlertWorkerImage"/>
    /// and <inheritdoc cref="FlareContainerImageTags.DashboardImage"/>, all with the tag <inheritdoc cref="FlareContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="clickhouse">The ClickHouse server Flare stores its telemetry in. Flare adds its own database to it.</param>
    /// <param name="redis">The Redis instance Flare uses to buffer telemetry before it is written to ClickHouse.</param>
    /// <param name="dashboardPort">The host port for the Flare dashboard.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// The Flare images create and migrate their own ClickHouse schema on startup.
    /// <example>
    /// Add Flare to the application model and send a project's telemetry to it.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var clickhouse = builder.AddClickHouse("clickhouse");
    /// var redis = builder.AddRedis("redis");
    /// var flare = builder.AddFlare("flare", clickhouse, redis);
    ///
    /// builder.AddProject&lt;Projects.Api&gt;("api")
    ///     .WithOtlpExporter(flare)
    ///     .WaitFor(flare);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<FlareResource> AddFlare(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ClickHouseServerResource> clickhouse,
        IResourceBuilder<RedisResource> redis,
        int? dashboardPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(clickhouse);
        ArgumentNullException.ThrowIfNull(redis);

        var database = clickhouse.AddDatabase($"{name}-clickhousedb", ClickHouseDatabaseName);

        var flareResource = new FlareResource(name);
        var apiResource = new FlareApiResource($"{name}-api", flareResource);
        var alertWorkerResource = new FlareAlertWorkerResource($"{name}-alert-worker", flareResource);
        var dashboardResource = new FlareDashboardResource($"{name}-dashboard", flareResource);
        flareResource.Api = apiResource;
        flareResource.AlertWorker = alertWorkerResource;
        flareResource.Dashboard = dashboardResource;

        // Ingest: the OTLP ports are fixed and unproxied on purpose, so OTLP clients outside the
        // AppHost can use the conventional 4317/4318 without going through Aspire's proxy.
        var flare = builder.AddResource(flareResource)
            .WithEndpoint(port: OtlpGrpcPort, targetPort: OtlpGrpcPort, scheme: "http", name: FlareResource.OtlpGrpcEndpointName, isProxied: false)
            .WithEndpoint(FlareResource.OtlpGrpcEndpointName, e => e.Transport = "http2", createIfNotExists: false)
            .WithEndpoint(port: OtlpHttpPort, targetPort: OtlpHttpPort, scheme: "http", name: FlareResource.OtlpHttpEndpointName, isProxied: false)
            .WithImage(FlareContainerImageTags.IngestImage, FlareContainerImageTags.Tag)
            .WithImageRegistry(FlareContainerImageTags.Registry)
            .WithReference(database, ClickHouseConnectionName)
            .WaitFor(database)
            .WithReference(redis, RedisConnectionName)
            .WaitFor(redis)
            .WithEnvironment("Identity__DbPath", IdentityDbPath)
            .WithHttpHealthCheck("/health", endpointName: FlareResource.OtlpHttpEndpointName)
            .WithIconName("DataTrending");

        var api = builder.AddResource(apiResource)
            .WithHttpEndpoint(port: null, targetPort: ApiPort, name: FlareApiResource.HttpEndpointName)
            .WithImage(FlareContainerImageTags.ApiImage, FlareContainerImageTags.Tag)
            .WithImageRegistry(FlareContainerImageTags.Registry)
            .WithReference(database, ClickHouseConnectionName)
            .WaitFor(database)
            .WithReference(redis, RedisConnectionName)
            .WaitFor(redis)
            .WithEnvironment("Identity__DbPath", IdentityDbPath)
            .WithHttpHealthCheck("/health")
            .WithParentRelationship(flare)
            .WithIconName("Server");

        builder.AddResource(alertWorkerResource)
            .WithHttpEndpoint(port: null, targetPort: ApiPort, name: "http")
            .WithImage(FlareContainerImageTags.AlertWorkerImage, FlareContainerImageTags.Tag)
            .WithImageRegistry(FlareContainerImageTags.Registry)
            .WithReference(database, ClickHouseConnectionName)
            .WaitFor(database)
            .WithReference(redis, RedisConnectionName)
            .WaitFor(redis)
            .WithHttpHealthCheck("/health")
            .WithParentRelationship(flare)
            .WithIconName("AlertOn");

        // The dashboard's JavaScript runs in the user's browser, so both URLs below are pinned to the
        // localhost network: the default container-network address would not resolve from the host.
        var apiBrowserEndpoint = api.GetEndpoint(FlareApiResource.HttpEndpointName, KnownNetworkIdentifiers.LocalhostNetwork);
        var dashboard = builder.AddResource(dashboardResource)
            .WithHttpEndpoint(port: dashboardPort, targetPort: DashboardPort, name: FlareDashboardResource.HttpEndpointName)
            .WithImage(FlareContainerImageTags.DashboardImage, FlareContainerImageTags.Tag)
            .WithImageRegistry(FlareContainerImageTags.Registry)
            .WithEnvironment("PUBLIC_API_URL", ReferenceExpression.Create($"{apiBrowserEndpoint}"))
            .WaitFor(api)
            .WithHttpHealthCheck("/")
            .WithParentRelationship(flare)
            .WithIconName("ChartMultiple");

        // The API rejects every browser origin it has not been told about, so without this the
        // dashboard cannot call it.
        var dashboardBrowserEndpoint = dashboard.GetEndpoint(FlareDashboardResource.HttpEndpointName, KnownNetworkIdentifiers.LocalhostNetwork);
        api.WithEnvironment("Cors__AllowedOrigins__0", ReferenceExpression.Create($"{dashboardBrowserEndpoint}"));

        database.WithParentRelationship(flare);

        return flare;
    }

    /// <summary>
    /// Persists Flare's identity database (dashboard users, sessions and ingest API keys) in a volume shared
    /// by the ingest service and the query API. Without it, both keep a private identity database that is
    /// lost when their containers are recreated.
    /// </summary>
    /// <ats-summary>Persists Flare's identity database in a shared volume</ats-summary>
    /// <param name="builder">The Flare resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to a name derived from the application and resource name.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// ClickHouse and Redis keep their own data volumes; configure them on those resources.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<FlareResource> WithDataVolumes(
        this IResourceBuilder<FlareResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var volumeName = name ?? VolumeNameGenerator.Generate(builder, "identity");
        var resource = builder.Resource;

        // Both containers must see the same file, which is why they mount the same named volume.
        builder.WithVolume(volumeName, IdentityMountPath);
        builder.ApplicationBuilder.CreateResourceBuilder(resource.Api).WithVolume(volumeName, IdentityMountPath);

        return builder;
    }

    /// <summary>
    /// Sets the host ports of the ingest service's OTLP endpoints. By default they are the conventional
    /// 4317 (gRPC) and 4318 (HTTP).
    /// </summary>
    /// <param name="builder">The Flare resource builder.</param>
    /// <param name="grpcPort">The host port for OTLP/gRPC, or <see langword="null"/> to leave it unchanged.</param>
    /// <param name="httpPort">The host port for OTLP/HTTP, or <see langword="null"/> to leave it unchanged.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<FlareResource> WithIngestPorts(
        this IResourceBuilder<FlareResource> builder,
        int? grpcPort = null,
        int? httpPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (grpcPort is not null)
        {
            builder.WithEndpoint(FlareResource.OtlpGrpcEndpointName, e => e.Port = grpcPort, createIfNotExists: false);
        }

        if (httpPort is not null)
        {
            builder.WithEndpoint(FlareResource.OtlpHttpEndpointName, e => e.Port = httpPort, createIfNotExists: false);
        }

        return builder;
    }

    /// <summary>
    /// Requires an API key on the ingest service. OTLP clients must send it as
    /// <c>Authorization: Bearer &lt;key&gt;</c>, for example through
    /// <c>OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer &lt;key&gt;</c>. Resources wired with
    /// <see cref="WithOtlpExporter{T}(IResourceBuilder{T}, IResourceBuilder{FlareResource}, OtlpProtocol)"/> send it automatically.
    /// </summary>
    /// <param name="builder">The Flare resource builder.</param>
    /// <param name="apiKey">The parameter holding the API key.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<FlareResource> WithApiKey(
        this IResourceBuilder<FlareResource> builder,
        IResourceBuilder<ParameterResource> apiKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiKey);

        builder.Resource.IngestApiKey = apiKey.Resource;

        return builder
            .WithEnvironment("Auth__IngestKeyRequired", "true")
            .WithEnvironment("Auth__StaticIngestApiKey", apiKey);
    }

    /// <summary>
    /// Sets the URLs a browser uses to reach the query API and the dashboard. By default these are the
    /// loopback addresses of the local containers, which only works when the browser runs on the same
    /// machine. Set them when the stack is published and reached through other hostnames.
    /// </summary>
    /// <param name="builder">The Flare resource builder.</param>
    /// <param name="apiUrl">The parameter holding the public URL of the query API, or <see langword="null"/> to leave it unchanged.</param>
    /// <param name="dashboardUrl">
    /// The parameter holding the public URL of the dashboard, or <see langword="null"/> to leave it unchanged.
    /// The query API uses this value verbatim as its allowed CORS origin, so it must be an origin only:
    /// <c>scheme://host[:port]</c>, with no path and no trailing slash (for example <c>https://flare.example.com</c>).
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<FlareResource> WithPublicUrls(
        this IResourceBuilder<FlareResource> builder,
        IResourceBuilder<ParameterResource>? apiUrl = null,
        IResourceBuilder<ParameterResource>? dashboardUrl = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var resource = builder.Resource;

        if (apiUrl is not null)
        {
            builder.ApplicationBuilder.CreateResourceBuilder(resource.Dashboard).WithEnvironment("PUBLIC_API_URL", apiUrl);
        }

        if (dashboardUrl is not null)
        {
            // Alerts link back to the dashboard, so the alert worker needs the URL as well as the API.
            builder.ApplicationBuilder.CreateResourceBuilder(resource.Api)
                .WithEnvironment("Cors__AllowedOrigins__0", dashboardUrl)
                .WithEnvironment("Alerting__PublicUrl", dashboardUrl);
            builder.ApplicationBuilder.CreateResourceBuilder(resource.AlertWorker)
                .WithEnvironment("Alerting__PublicUrl", dashboardUrl);
        }

        return builder;
    }

    /// <summary>
    /// Sets the image tag of all four Flare containers (ingest, API, alert worker and dashboard).
    /// </summary>
    /// <param name="builder">The Flare resource builder.</param>
    /// <param name="tag">The image tag, for example <c>0.6</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<FlareResource> WithFlareImageTag(
        this IResourceBuilder<FlareResource> builder,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(tag);

        var resource = builder.Resource;
        var applicationBuilder = builder.ApplicationBuilder;

        builder.WithImageTag(tag);
        applicationBuilder.CreateResourceBuilder(resource.Api).WithImageTag(tag);
        applicationBuilder.CreateResourceBuilder(resource.AlertWorker).WithImageTag(tag);
        applicationBuilder.CreateResourceBuilder(resource.Dashboard).WithImageTag(tag);

        return builder;
    }

    /// <summary>
    /// Points a resource's OpenTelemetry exporter at a Flare ingest service by setting
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> and <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>.
    /// </summary>
    /// <remarks>
    /// The resource's telemetry goes to Flare instead of the Aspire dashboard, so it no longer appears in the
    /// dashboard's logs, traces and metrics views. The Aspire dashboard's OTLP API key is removed from
    /// <c>OTEL_EXPORTER_OTLP_HEADERS</c> so it is not sent to Flare; other headers are kept. When the Flare resource
    /// has an API key (see <see cref="WithApiKey"/>), an <c>Authorization=Bearer &lt;key&gt;</c> header is added.
    /// </remarks>
    /// <typeparam name="T">The type of the resource that will export telemetry.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="flare">The Flare resource builder.</param>
    /// <param name="protocol">
    /// The OTLP protocol to export with. <see cref="OtlpProtocol.Grpc"/> uses the ingest service's gRPC endpoint;
    /// <see cref="OtlpProtocol.HttpProtobuf"/> and <see cref="OtlpProtocol.HttpJson"/> use its HTTP endpoint.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "Generic receiver; the polyglot overload is withFlareOtlpExporter.")]
    public static IResourceBuilder<T> WithOtlpExporter<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<FlareResource> flare,
        OtlpProtocol protocol = OtlpProtocol.Grpc)
        where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(flare);

        var flareResource = flare.Resource;
        var (endpoint, protocolName) = protocol switch
        {
            OtlpProtocol.Grpc => (flareResource.OtlpGrpcEndpoint, "grpc"),
            OtlpProtocol.HttpProtobuf => (flareResource.OtlpHttpEndpoint, "http/protobuf"),
            OtlpProtocol.HttpJson => (flareResource.OtlpHttpEndpoint, "http/json"),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported OTLP protocol."),
        };

        return builder
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint)
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", protocolName)
            .WithEnvironment(context => SetOtlpHeaders(context, flareResource));
    }

    // Aspire adds its dashboard's OTLP key to OTEL_EXPORTER_OTLP_HEADERS; once the exporter points at Flare
    // that key must not travel with it. Keeps any other headers and adds Flare's own key when one is set.
    private static void SetOtlpHeaders(EnvironmentCallbackContext context, FlareResource flare)
    {
        var headers = new List<string>();
        object? untouched = null;

        if (context.EnvironmentVariables.TryGetValue(OtlpHeadersVariable, out var existing))
        {
            if (existing is string text)
            {
                headers.AddRange(text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(header => !header.StartsWith(DashboardApiKeyHeader + "=", StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                // A value the user built from parameters or expressions: leave it as it is.
                untouched = existing;
            }
        }

        // A user-built value is only combined with Flare's key when Aspire can render it inside an expression.
        if (untouched is not null && (flare.IngestApiKey is null || untouched is not (IValueProvider and IManifestExpressionProvider)))
        {
            return;
        }

        if (flare.IngestApiKey is null)
        {
            if (headers.Count == 0)
            {
                context.EnvironmentVariables.Remove(OtlpHeadersVariable);
            }
            else
            {
                context.EnvironmentVariables[OtlpHeadersVariable] = string.Join(',', headers);
            }

            return;
        }

        var value = new ReferenceExpressionBuilder();
        if (untouched is not null)
        {
            value.AppendValueProvider(untouched);
            value.AppendLiteral(",");
        }

        foreach (var header in headers)
        {
            value.AppendLiteral(header + ",");
        }

        value.AppendLiteral("Authorization=Bearer ");
        value.AppendValueProvider(flare.IngestApiKey);
        context.EnvironmentVariables[OtlpHeadersVariable] = value.Build();
    }

    /// <summary>
    /// Points a resource's OpenTelemetry exporter at a Flare ingest service by setting
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> and <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="flare">The Flare resource builder.</param>
    /// <param name="protocol">
    /// The OTLP protocol to export with. <see cref="OtlpProtocol.Grpc"/> uses the ingest service's gRPC endpoint;
    /// <see cref="OtlpProtocol.HttpProtobuf"/> and <see cref="OtlpProtocol.HttpJson"/> use its HTTP endpoint.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport("withFlareOtlpExporter", MethodName = "withFlareOtlpExporter")]
    internal static IResourceBuilder<IResourceWithEnvironment> WithFlareOtlpExporterPolyglot(
        this IResourceBuilder<IResourceWithEnvironment> builder,
        IResourceBuilder<FlareResource> flare,
        OtlpProtocol protocol = OtlpProtocol.Grpc) => builder.WithOtlpExporter(flare, protocol);

    // Groups a resource under the Flare resource in the dashboard. This is the only parent a Flare
    // child may have, so the parent is typed as FlareResource rather than any IResource.
    // The children deliberately do not implement IResourceWithParent<FlareResource>: Aspire forbids a
    // resource from waiting on its parent, and the dashboard waits on the API.
    internal static IResourceBuilder<T> WithParentRelationship<T>(
        this IResourceBuilder<T> child,
        IResourceBuilder<FlareResource> parent)
        where T : IResource
        => ResourceBuilderExtensions.WithParentRelationship(child, parent);
}
