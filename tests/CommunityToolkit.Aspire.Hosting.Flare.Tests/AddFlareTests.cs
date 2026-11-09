// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flare.Tests;

public class AddFlareTests
{
    private static IResourceBuilder<FlareResource> CreateFlare(IDistributedApplicationBuilder builder) =>
        builder.AddFlare("flare", builder.AddClickHouse("clickhouse"), builder.AddRedis("redis"));

    [Fact]
    public void AddFlareAddsTheWholeStack()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        CreateFlare(builder);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var flare = Assert.Single(appModel.Resources.OfType<FlareResource>());
        Assert.Equal("flare", flare.Name);
        Assert.Equal("flare-api", Assert.Single(appModel.Resources.OfType<FlareApiResource>()).Name);
        Assert.Equal("flare-alert-worker", Assert.Single(appModel.Resources.OfType<FlareAlertWorkerResource>()).Name);
        Assert.Equal("flare-dashboard", Assert.Single(appModel.Resources.OfType<FlareDashboardResource>()).Name);

        var database = Assert.Single(appModel.Resources.OfType<ClickHouseDatabaseResource>());
        Assert.Equal("flare-clickhousedb", database.Name);
        Assert.Equal("clickhousedb", database.DatabaseName);
    }

    [Fact]
    public void AddFlareUsesPinnedImagesWithoutLatestTag()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        CreateFlare(builder);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        AssertImage(appModel.Resources.OfType<FlareResource>().Single(), FlareContainerImageTags.IngestImage);
        AssertImage(appModel.Resources.OfType<FlareApiResource>().Single(), FlareContainerImageTags.ApiImage);
        AssertImage(appModel.Resources.OfType<FlareAlertWorkerResource>().Single(), FlareContainerImageTags.AlertWorkerImage);
        AssertImage(appModel.Resources.OfType<FlareDashboardResource>().Single(), FlareContainerImageTags.DashboardImage);

        Assert.NotEqual("latest", FlareContainerImageTags.Tag);

        static void AssertImage(IResource resource, string image)
        {
            var annotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());
            Assert.Equal(image, annotation.Image);
            Assert.Equal(FlareContainerImageTags.Tag, annotation.Tag);
            Assert.Equal(FlareContainerImageTags.Registry, annotation.Registry);
        }
    }

    [Fact]
    public void AddFlareExposesUnproxiedOtlpEndpoints()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder);

        var grpc = Assert.Single(flare.Resource.Annotations.OfType<EndpointAnnotation>(), e => e.Name == "otlp-grpc");
        Assert.Equal(4317, grpc.TargetPort);
        Assert.Equal(4317, grpc.Port); // set explicitly: Aspire 17 assigns a random host port to unproxied endpoints otherwise
        Assert.False(grpc.IsProxied);
        Assert.Equal("http2", grpc.Transport);

        var http = Assert.Single(flare.Resource.Annotations.OfType<EndpointAnnotation>(), e => e.Name == "otlp-http");
        Assert.Equal(4318, http.TargetPort);
        Assert.Equal(4318, http.Port);
        Assert.False(http.IsProxied);
    }

    [Fact]
    public void AddFlareUsesDashboardPort()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = builder.AddFlare("flare", builder.AddClickHouse("clickhouse"), builder.AddRedis("redis"), dashboardPort: 5555);

        var endpoint = Assert.Single(flare.Resource.Dashboard.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(3000, endpoint.TargetPort);
        Assert.Equal(5555, endpoint.Port);
    }

    [Fact]
    public async Task AddFlareWiresIngestToClickHouseAndRedis()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder);

        var config = await flare.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("{flare-clickhousedb.connectionString}", config["ConnectionStrings__clickhousedb"]);
        Assert.Equal("{redis.connectionString}", config["ConnectionStrings__redis"]);
        Assert.Equal("/data/identity/flare-identity.db", config["Identity__DbPath"]);
    }

    [Fact]
    public void AddFlareParentsTheOtherResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder);

        Assert.Same(flare.Resource, flare.Resource.Api.Parent);
        Assert.Same(flare.Resource, flare.Resource.AlertWorker.Parent);
        Assert.Same(flare.Resource, flare.Resource.Dashboard.Parent);

        var database = Assert.Single(builder.Resources.OfType<ClickHouseDatabaseResource>());
        IResource[] children = [flare.Resource.Api, flare.Resource.AlertWorker, flare.Resource.Dashboard, database];
        foreach (var child in children)
        {
            var relationship = Assert.Single(child.Annotations.OfType<ResourceRelationshipAnnotation>(), r => r.Type == "Parent");
            Assert.Same(flare.Resource, relationship.Resource);
        }
    }

    [Fact]
    public async Task AddFlarePointsTheBrowserFacingUrlsAtLocalhostEndpoints()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder);

        var dashboardConfig = await flare.Resource.Dashboard.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);
        var apiConfig = await flare.Resource.Api.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("{flare-api.bindings.http.url}", dashboardConfig["PUBLIC_API_URL"]);
        Assert.Equal("{flare-dashboard.bindings.http.url}", apiConfig["Cors__AllowedOrigins__0"]);
    }

    [Fact]
    public void AddFlareRegistersHealthChecks()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder);

        Assert.Single(flare.Resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Single(flare.Resource.Api.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Single(flare.Resource.AlertWorker.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Single(flare.Resource.Dashboard.Annotations.OfType<HealthCheckAnnotation>());
    }

    [Fact]
    public async Task ConnectionStringIsTheOtlpGrpcUrl()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder);

        Assert.Equal(
            $"{{{flare.Resource.Name}.bindings.otlp-grpc.url}}",
            flare.Resource.ConnectionStringExpression.ValueExpression);
        Assert.Equal(3, ((IResourceWithConnectionString)flare.Resource).GetConnectionProperties().Count());
        await Task.CompletedTask;
    }

    [Fact]
    public void WithDataVolumesMountsOneVolumeIntoIngestAndApi()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder).WithDataVolumes("flare-identity");

        var ingestMount = Assert.Single(flare.Resource.Annotations.OfType<ContainerMountAnnotation>());
        var apiMount = Assert.Single(flare.Resource.Api.Annotations.OfType<ContainerMountAnnotation>());

        Assert.Equal("flare-identity", ingestMount.Source);
        Assert.Equal("flare-identity", apiMount.Source);
        Assert.Equal("/data/identity", ingestMount.Target);
        Assert.Equal("/data/identity", apiMount.Target);
        Assert.Equal(ContainerMountType.Volume, ingestMount.Type);
        Assert.Empty(flare.Resource.AlertWorker.Annotations.OfType<ContainerMountAnnotation>());
    }

    [Fact]
    public void WithoutDataVolumesNothingIsMounted()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder);

        Assert.Empty(flare.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Empty(flare.Resource.Api.Annotations.OfType<ContainerMountAnnotation>());
    }

    [Fact]
    public void WithIngestPortsOverridesTheHostPorts()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder).WithIngestPorts(grpcPort: 14317);

        Assert.Equal(14317, Assert.Single(flare.Resource.Annotations.OfType<EndpointAnnotation>(), e => e.Name == "otlp-grpc").Port);
        Assert.Equal(4318, Assert.Single(flare.Resource.Annotations.OfType<EndpointAnnotation>(), e => e.Name == "otlp-http").Port);
    }

    [Fact]
    public async Task WithApiKeyRequiresAnIngestKey()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("flare-key", "secret-value", secret: true);

        var flare = CreateFlare(builder).WithApiKey(apiKey);

        var config = await flare.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("true", config["Auth__IngestKeyRequired"]);
        Assert.Equal("{flare-key.value}", config["Auth__StaticIngestApiKey"]);
    }

    [Fact]
    public async Task WithPublicUrlsOverridesTheBrowserFacingUrls()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiUrl = builder.AddParameter("api-url", "https://flare-api.example.com");
        var dashboardUrl = builder.AddParameter("dashboard-url", "https://flare.example.com");

        var flare = CreateFlare(builder).WithPublicUrls(apiUrl, dashboardUrl);

        var dashboardConfig = await flare.Resource.Dashboard.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);
        var apiConfig = await flare.Resource.Api.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);
        var workerConfig = await flare.Resource.AlertWorker.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("{api-url.value}", dashboardConfig["PUBLIC_API_URL"]);
        Assert.Equal("{dashboard-url.value}", apiConfig["Cors__AllowedOrigins__0"]);
        Assert.Equal("{dashboard-url.value}", apiConfig["Alerting__PublicUrl"]);
        Assert.Equal("{dashboard-url.value}", workerConfig["Alerting__PublicUrl"]);
    }

    [Fact]
    public void WithFlareImageTagAppliesToAllFourContainers()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var flare = CreateFlare(builder).WithFlareImageTag("0.7");

        foreach (var resource in new IResource[] { flare.Resource, flare.Resource.Api, flare.Resource.AlertWorker, flare.Resource.Dashboard })
        {
            Assert.Equal("0.7", Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>()).Tag);
        }
    }

    [Theory]
    [InlineData(false, "grpc", "otlp-grpc")]
    [InlineData(true, "http/protobuf", "otlp-http")]
    public async Task WithOtlpExporterPointsTheConsumerAtFlare(bool useHttp, string protocol, string endpointName)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var flare = CreateFlare(builder);

        var app = builder.AddContainer("app", "image").WithOtlpExporter(flare, useHttp);

        var config = await app.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal($"{{flare.bindings.{endpointName}.url}}", config["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        Assert.Equal(protocol, config["OTEL_EXPORTER_OTLP_PROTOCOL"]);
    }

    // Stands in for the header Aspire adds for its own dashboard in run mode.
    private const string DashboardHeaders = "x-otlp-api-key=dashboard-secret,x-tenant=shop";

    [Fact]
    public async Task WithOtlpExporterDropsTheDashboardKeyAndKeepsOtherHeaders()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var flare = CreateFlare(builder);

        var app = builder.AddContainer("app", "image")
            .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", DashboardHeaders)
            .WithOtlpExporter(flare);

        var config = await app.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("x-tenant=shop", config["OTEL_EXPORTER_OTLP_HEADERS"]);
    }

    [Fact]
    public async Task WithOtlpExporterRemovesTheHeadersVariableWhenOnlyTheDashboardKeyWasSet()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var flare = CreateFlare(builder);

        var app = builder.AddContainer("app", "image")
            .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", "x-otlp-api-key=dashboard-secret")
            .WithOtlpExporter(flare);

        var config = await app.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.False(config.ContainsKey("OTEL_EXPORTER_OTLP_HEADERS"));
    }

    [Fact]
    public async Task WithOtlpExporterSendsFlaresApiKeyAsBearer()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("flare-key", "secret-value", secret: true);
        var flare = CreateFlare(builder);

        var app = builder.AddContainer("app", "image")
            .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", DashboardHeaders)
            .WithOtlpExporter(flare);

        // Set after the exporter on purpose: the header is resolved when the environment is built.
        flare.WithApiKey(apiKey);

        var config = await app.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("x-tenant=shop,Authorization=Bearer {flare-key.value}", config["OTEL_EXPORTER_OTLP_HEADERS"]);
    }
}
