using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void CanCreateTheCollectorResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddOpenTelemetryCollector("collector")
            .WithConfig("./config.yaml")
            .WithAppForwarding();
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();

        Assert.NotNull(collectorResource);

        Assert.Equal("collector", collectorResource.Name);
    }

    [Fact]
    public async Task CanCreateTheCollectorResourceWithCustomConfig()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOpenTelemetryCollector("collector")
            .WithConfig("./config.yaml")
            .WithAppForwarding();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);
        var configMount = collectorResource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(configMount);
        Assert.EndsWith("config.yaml", configMount.Source);
        Assert.Equal("/config/config.yaml", configMount.Target);

        var args = collectorResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().SingleOrDefault();
        Assert.NotNull(args);
        CommandLineArgsCallbackContext context = new([]);
        var argValues = args.Callback(context);
        await argValues;

        Assert.Contains("--config=/config/config.yaml", context.Args);
    }

    [Fact]
    public async Task CanCreateTheCollectorResourceWithMultipleConfigs()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOpenTelemetryCollector("collector")
            .WithConfig("./config.yaml")
            .WithConfig("./config2.yaml")
            .WithAppForwarding();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var configMounts = collectorResource.Annotations.OfType<ContainerMountAnnotation>().ToList();
        Assert.Equal(2, configMounts.Count);
        Assert.Collection(configMounts,
            m =>
            {
                Assert.EndsWith("config.yaml", m.Source);
                Assert.Equal("/config/config.yaml", m.Target);
            },
            m =>
            {
                Assert.EndsWith("config2.yaml", m.Source);
                Assert.Equal("/config/config2.yaml", m.Target);
            });

        var args = collectorResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>();
        Assert.NotNull(args);
        CommandLineArgsCallbackContext context = new([]);
        foreach (var arg in args)
        {
            var argValues = arg.Callback(context);
            await argValues;
        }

        Assert.Contains("--config=/config/config.yaml", context.Args);
        Assert.Contains("--config=/config/config2.yaml", context.Args);
    }

    [Fact]
    public void CanDisableGrpcEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOpenTelemetryCollector("collector", settings => settings.EnableGrpcEndpoint = false)
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var endpoints = collectorResource.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.DoesNotContain(endpoints, e => e.Name == "grpc");
        Assert.Contains(endpoints, e => e.Name == "http");
    }

    [Fact]
    public void CanDisableHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOpenTelemetryCollector("collector", settings => settings.EnableHttpEndpoint = false)
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var endpoints = collectorResource.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.Contains(endpoints, e => e.Name == "grpc");
        Assert.DoesNotContain(endpoints, e => e.Name == "http");
    }

    [Fact]
    public void CanDisableBothEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.EnableHttpEndpoint = false;
            settings.EnableGrpcEndpoint = false;
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        Assert.Empty(collectorResource.Annotations.OfType<EndpointAnnotation>());
    }

    [Fact]
    public void ContainerHasAspireEnvironmentVariables()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var envs = collectorResource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envs);

        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));
        foreach (var env in envs)
        {
            env.Callback(context);
        }

        Assert.Contains("ASPIRE_ENDPOINT", context.EnvironmentVariables.Keys);
        Assert.Contains("ASPIRE_API_KEY", context.EnvironmentVariables.Keys);
        Assert.Equal("http://host.docker.internal:18889", context.EnvironmentVariables["ASPIRE_ENDPOINT"]);
        Assert.NotNull(context.EnvironmentVariables["ASPIRE_API_KEY"]);
    }

    [Fact]
    public void CanForceNonSSLForTheCollector()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings => settings.ForceNonSecureReceiver = true)
            .WithAppForwarding();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var endpoints = collectorResource.Annotations.OfType<EndpointAnnotation>().ToList();
        var grpcEndpoint = endpoints.Single(e => e.Name == "grpc");
        var httpEndpoint = endpoints.Single(e => e.Name == "http");
        Assert.Equal("http", grpcEndpoint.UriScheme);
        Assert.Equal("http", httpEndpoint.UriScheme);
    }
}
