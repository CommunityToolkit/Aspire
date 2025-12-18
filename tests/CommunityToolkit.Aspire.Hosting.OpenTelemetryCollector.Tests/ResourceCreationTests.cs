using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.Tests;

public class ResourceCreationTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void CanCreateTheCollectorResource()
    {
        var builder = TestDistributedApplicationBuilder.Create();

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
        builder.AddOpenTelemetryCollector("collector", settings =>
        {

            settings.DisableHealthcheck = true;
        })
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
            settings.DisableHealthcheck = true;
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        Assert.Empty(collectorResource.Annotations.OfType<EndpointAnnotation>());
    }

    [Fact]
    [RequiresDocker]
    public async Task ContainerHasAspireEnvironmentVariables()
    {
        using var builder = TestDistributedApplicationBuilder.Create()
            .WithTestAndResourceLogging(testOutputHelper);
        builder.Configuration["APPHOST:ContainerHostname"] = "what.ever";

        var collector = builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.DisableHealthcheck = true;
        })
            .WithAppForwarding();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await app.StartAsync();
        await resourceNotificationService.WaitForResourceHealthyAsync(collector.Resource.Name);

        Assert.True(resourceNotificationService.TryGetCurrentState(collector.Resource.Name, out var resourceEvent));

        var envVars = resourceEvent.Snapshot.EnvironmentVariables.ToDictionary(k => k.Name, v => v.Value);

        var endpoint = Assert.Contains("ASPIRE_ENDPOINT", envVars);
        var apiKey = Assert.Contains("ASPIRE_API_KEY", envVars);

        Assert.Equal($"http://host.docker.internal:4317", endpoint);
        Assert.NotNull(apiKey);
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

    [Fact]
    public void CollectorUsesCustomImageAndTag()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.CollectorTag = "mytag";
            settings.Registry = "myregistry.io";
            settings.Image = "myorg/mycollector";
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        Assert.True(collectorResource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.NotNull(imageAnnotations);
        Assert.Equal("mytag", imageAnnotations.Tag);
        Assert.Equal("myregistry.io/myorg/mycollector", imageAnnotations.Image);
        // Registry is likely set to null/empty when the full path is provided as image
        Assert.Null(imageAnnotations.Registry);
    }

    [Fact]
    public void CollectorEndpointsUseHttpsWhenDashboardIsHttps()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var endpoints = collectorResource.Annotations.OfType<EndpointAnnotation>().ToList();
        var grpcEndpoint = endpoints.Single(e => e.Name == "grpc");
        var httpEndpoint = endpoints.Single(e => e.Name == "http");
        Assert.Equal("https", grpcEndpoint.UriScheme);
        Assert.Equal("https", httpEndpoint.UriScheme);
    }

    [Fact]
    public void CanConfigureOnlyGrpcEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.EnableGrpcEndpoint = true;
            settings.EnableHttpEndpoint = false;
            settings.DisableHealthcheck = true;
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var endpoints = collectorResource.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.Single(endpoints);
        var grpcEndpoint = endpoints.Single();
        Assert.Equal("grpc", grpcEndpoint.Name);
        Assert.Equal(4317, grpcEndpoint.TargetPort);
        Assert.Equal("http", grpcEndpoint.UriScheme);
    }

    [Fact]
    public void CanConfigureOnlyHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.EnableGrpcEndpoint = false;
            settings.EnableHttpEndpoint = true;
            settings.DisableHealthcheck = true;
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var endpoints = collectorResource.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.Single(endpoints);
        var httpEndpoint = endpoints.Single();
        Assert.Equal("http", httpEndpoint.Name);
        Assert.Equal(4318, httpEndpoint.TargetPort);
        Assert.Equal("http", httpEndpoint.UriScheme);
    }

    [Fact]
    public void ForceNonSecureReceiverOverridesHttpsEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = true;
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var endpoints = collectorResource.Annotations.OfType<EndpointAnnotation>().ToList();
        var grpcEndpoint = endpoints.Single(e => e.Name == "grpc");
        var httpEndpoint = endpoints.Single(e => e.Name == "http");

        // Even though dashboard is HTTPS, ForceNonSecureReceiver should make endpoints HTTP
        Assert.Equal("http", grpcEndpoint.UriScheme);
        Assert.Equal("http", httpEndpoint.UriScheme);
    }

    [Fact]
    public void DevCertificateLogicIsNotTriggeredWhenForceNonSecureReceiverEnabled()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = true; // Force HTTP even with HTTPS dashboard
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        // Check that no certificate-related arguments were added
        var args = collectorResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList();
        var context = new CommandLineArgsCallbackContext([]);
        foreach (var arg in args)
        {
            arg.Callback(context);
        }

        // Should not contain TLS certificate configuration args
        Assert.DoesNotContain(context.Args.Cast<string>(), a => a.Contains("receivers::otlp::protocols::http::tls::cert_file"));
        Assert.DoesNotContain(context.Args.Cast<string>(), a => a.Contains("receivers::otlp::protocols::grpc::tls::cert_file"));
    }

    [Fact]
    public void RunWithHttpsDevCertificateAddsExecutableResourceInRunMode()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = false; // Allow HTTPS
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should have created a dev-cert-export executable resource
        var devCertExportResource = appModel.Resources.OfType<ExecutableResource>()
            .SingleOrDefault(r => r.Name == "dev-cert-export");
        Assert.NotNull(devCertExportResource);

        // Verify it's configured to run dotnet dev-certs
        var args = devCertExportResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList();
        Assert.NotEmpty(args);

        var context = new CommandLineArgsCallbackContext([]);
        foreach (var arg in args)
        {
            arg.Callback(context);
        }

        Assert.Contains("dev-certs", context.Args.Cast<string>());
        Assert.Contains("https", context.Args.Cast<string>());
        Assert.Contains("--export-path", context.Args.Cast<string>());
        Assert.Contains("--format", context.Args.Cast<string>());
        Assert.Contains("Pem", context.Args.Cast<string>());
        Assert.Contains("--no-password", context.Args.Cast<string>());
    }

    [Fact]
    public void RunWithHttpsDevCertificateAddsContainerFilesAndWaitAnnotation()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = false; // Allow HTTPS
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        // Should have a WaitAnnotation for the dev-cert-export resource
        var waitAnnotations = collectorResource.Annotations.OfType<WaitAnnotation>().ToList();
        var devCertWaitAnnotation = waitAnnotations.FirstOrDefault(w => w.Resource.Name == "dev-cert-export");
        Assert.NotNull(devCertWaitAnnotation);
        Assert.Equal(WaitType.WaitForCompletion, devCertWaitAnnotation.WaitType);

        // Should have a ContainerFilesAnnotation for the dev certificates
        var containerFilesAnnotations = collectorResource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().ToList();
        var devCertFilesAnnotation = containerFilesAnnotations.FirstOrDefault(cf => cf.DestinationPath == "/dev-certs");
        Assert.NotNull(devCertFilesAnnotation);
    }

    [Fact]
    public void RunWithHttpsDevCertificateNotTriggeredInNonRunMode()
    {
        // Use regular builder (not TestDistributedApplicationBuilder.Create) which defaults to non-Run mode
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = false; // Allow HTTPS
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should NOT have created a dev-cert-export executable resource
        var devCertExportResource = appModel.Resources.OfType<ExecutableResource>()
            .SingleOrDefault(r => r.Name == "dev-cert-export");
        Assert.Null(devCertExportResource);

        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        // Should NOT have container files annotation for dev certs
        var containerFilesAnnotations = collectorResource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().ToList();
        var devCertFilesAnnotation = containerFilesAnnotations.FirstOrDefault(cf => cf.DestinationPath == "/dev-certs");
        Assert.Null(devCertFilesAnnotation);
    }

    [Fact]
    public void RunWithHttpsDevCertificateNotTriggeredWhenForceNonSecureEnabled()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = true; // Force non-secure
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should NOT have created a dev-cert-export executable resource
        var devCertExportResource = appModel.Resources.OfType<ExecutableResource>()
            .SingleOrDefault(r => r.Name == "dev-cert-export");
        Assert.Null(devCertExportResource);

        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        // Should NOT have container files annotation for dev certs
        var containerFilesAnnotations = collectorResource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().ToList();
        var devCertFilesAnnotation = containerFilesAnnotations.FirstOrDefault(cf => cf.DestinationPath == "/dev-certs");
        Assert.Null(devCertFilesAnnotation);
    }

    [Fact]
    public void DevCertificateResourcesAddedWhenHttpsEnabled()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = false; // Allow HTTPS
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should have created a dev-cert-export executable resource
        var devCertExportResource = appModel.Resources.OfType<ExecutableResource>()
            .SingleOrDefault(r => r.Name == "dev-cert-export");
        Assert.NotNull(devCertExportResource);
        Assert.Equal("dotnet", devCertExportResource.Command);

        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        // Should have container files annotation for dev certs
        var containerFilesAnnotations = collectorResource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().ToList();
        var devCertFilesAnnotation = containerFilesAnnotations.FirstOrDefault(cf => cf.DestinationPath == "/dev-certs");
        Assert.NotNull(devCertFilesAnnotation);

        // Should have wait annotation for the dev-cert-export resource
        var waitAnnotations = collectorResource.Annotations.OfType<WaitAnnotation>().ToList();
        var devCertWaitAnnotation = waitAnnotations.FirstOrDefault(wa => wa.Resource == devCertExportResource);
        Assert.NotNull(devCertWaitAnnotation);
    }

    [Fact]
    public void DevCertificateContainerFilesOnlyAddedForEnabledEndpointsInRunMode()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.EnableGrpcEndpoint = true;
            settings.EnableHttpEndpoint = false; // Only enable gRPC
            settings.ForceNonSecureReceiver = false;
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        // Should have container files annotation for dev certs
        var containerFilesAnnotations = collectorResource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().ToList();
        var devCertFilesAnnotation = containerFilesAnnotations.FirstOrDefault(cf => cf.DestinationPath == "/dev-certs");
        Assert.NotNull(devCertFilesAnnotation);

        // Verify the TLS arguments are only added for enabled endpoints
        var args = collectorResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList();
        var context = new CommandLineArgsCallbackContext([]);
        foreach (var arg in args)
        {
            arg.Callback(context);
        }

        // Should only contain gRPC TLS args, not HTTP TLS args
        Assert.Contains(context.Args.Cast<string>(), a => a.Contains("receivers::otlp::protocols::grpc::tls::cert_file"));
        Assert.Contains(context.Args.Cast<string>(), a => a.Contains("receivers::otlp::protocols::grpc::tls::key_file"));
        Assert.DoesNotContain(context.Args.Cast<string>(), a => a.Contains("receivers::otlp::protocols::http::tls::cert_file"));
        Assert.DoesNotContain(context.Args.Cast<string>(), a => a.Contains("receivers::otlp::protocols::http::tls::key_file"));
    }

    [Fact]
    public void DevCertificateExecutableResourceHasCorrectConfiguration()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "https://localhost:18889";

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.ForceNonSecureReceiver = false; // Allow HTTPS
        })
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify the dev-cert-export executable has correct configuration
        var devCertExportResource = appModel.Resources.OfType<ExecutableResource>()
            .SingleOrDefault(r => r.Name == "dev-cert-export");
        Assert.NotNull(devCertExportResource);
        Assert.Equal("dotnet", devCertExportResource.Command);

        // Check the environment variable for consistent language
        var envAnnotations = devCertExportResource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envAnnotations);

        var envContext = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));
        foreach (var env in envAnnotations)
        {
            env.Callback(envContext);
        }

        Assert.Contains("DOTNET_CLI_UI_LANGUAGE", envContext.EnvironmentVariables.Keys);
        Assert.Equal("en", envContext.EnvironmentVariables["DOTNET_CLI_UI_LANGUAGE"]);

        // Check the arguments for certificate export
        var argsAnnotations = devCertExportResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList();
        Assert.NotEmpty(argsAnnotations);

        var argsContext = new CommandLineArgsCallbackContext([]);
        foreach (var arg in argsAnnotations)
        {
            arg.Callback(argsContext);
        }

        Assert.Contains("dev-certs", argsContext.Args);
        Assert.Contains("https", argsContext.Args);
        Assert.Contains("--export-path", argsContext.Args);
        Assert.Contains("--format", argsContext.Args);
        Assert.Contains("Pem", argsContext.Args);
        Assert.Contains("--no-password", argsContext.Args);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanDisableHealthcheckOnCollectorResource(bool disableHealthcheck)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddOpenTelemetryCollector("collector", settings =>
        {
            settings.DisableHealthcheck = disableHealthcheck;
        })
        .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var collectorResource = appModel.Resources.OfType<OpenTelemetryCollectorResource>().SingleOrDefault();
        Assert.NotNull(collectorResource);

        var hasHealthCheck = collectorResource.Annotations.OfType<HealthCheckAnnotation>().Any();
        if (disableHealthcheck)
        {
            Assert.False(hasHealthCheck);
        }
        else
        {
            Assert.True(hasHealthCheck);
            var argsAnnotations = collectorResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList();
            Assert.NotEmpty(argsAnnotations);

            var argsContext = new CommandLineArgsCallbackContext([]);
            foreach (var arg in argsAnnotations)
            {
                arg.Callback(argsContext);
            }

            Assert.Contains("--feature-gates=confmap.enableMergeAppendOption", argsContext.Args);
            Assert.Contains("--config=yaml:extensions::health_check/aspire::endpoint: 0.0.0.0:13233", argsContext.Args);
            Assert.Contains("--config=yaml:service::extensions: [ health_check/aspire ]", argsContext.Args);
        }

    }
}
