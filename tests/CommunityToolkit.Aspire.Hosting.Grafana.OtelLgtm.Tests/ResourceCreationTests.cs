using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Grafana.OtelLgtm.Tests;

public class ResourceCreationTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void CanCreateTheGrafanaOtelLgtmResource()
    {
        var builder = TestDistributedApplicationBuilder.Create();

        builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithAppForwarding();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("grafana-lgtm", resource.Name);
    }

    [Fact]
    public void ResourceHasCorrectImageAndTag()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotation));
        Assert.NotNull(imageAnnotation);
        Assert.Equal("grafana/otel-lgtm", imageAnnotation.Image);
        Assert.Equal("0.21.0", imageAnnotation.Tag);
    }

    [Fact]
    public void ResourceHasGrafanaEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        var grafana = endpoints.Single(e => e.Name == "http");
        Assert.Equal(3000, grafana.TargetPort);
        Assert.Equal("http", grafana.UriScheme);
    }

    [Fact]
    public void ResourceHasOtlpGrpcEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        var grpc = endpoints.Single(e => e.Name == "otel-grpc");
        Assert.Equal(4317, grpc.TargetPort);
        Assert.Equal("http", grpc.UriScheme);
    }

    [Fact]
    public void ResourceHasOtlpHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        var http = endpoints.Single(e => e.Name == "otel-http");
        Assert.Equal(4318, http.TargetPort);
        Assert.Equal("http", http.UriScheme);
    }

    [Fact]
    public void ResourceHasPrometheusEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        var prometheus = endpoints.Single(e => e.Name == "prometheus");
        Assert.Equal(9090, prometheus.TargetPort);
        Assert.Equal("http", prometheus.UriScheme);
    }

    [Fact]
    public void ResourceHasPyroscopeEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        var pyroscope = endpoints.Single(e => e.Name == "pyroscope");
        Assert.Equal(4040, pyroscope.TargetPort);
        Assert.Equal("http", pyroscope.UriScheme);
    }

    [Fact]
    public void CanSetFixedGrafanaPort()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm", grafanaPort: 3000);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        var grafana = endpoints.Single(e => e.Name == "http");
        Assert.Equal(3000, grafana.Port);
        Assert.Equal(3000, grafana.TargetPort);
    }

    [Fact]
    public void CanDisableGrpcEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm", configureSettings: settings =>
        {
            settings.EnableGrpcEndpoint = false;
        });

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.DoesNotContain(endpoints, e => e.Name == "otel-grpc");
        Assert.Contains(endpoints, e => e.Name == "otel-http");
    }

    [Fact]
    public void CanDisableHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm", configureSettings: settings =>
        {
            settings.EnableHttpEndpoint = false;
        });

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.Contains(endpoints, e => e.Name == "otel-grpc");
        Assert.DoesNotContain(endpoints, e => e.Name == "otel-http");
    }

    [Fact]
    public void CanCustomizeImageAndTag()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm", configureSettings: settings =>
        {
            settings.Image = "custom/otel-lgtm";
            settings.Tag = "1.0.0";
        });

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotation));
        Assert.NotNull(imageAnnotation);
        Assert.Equal("custom/otel-lgtm", imageAnnotation.Image);
        Assert.Equal("1.0.0", imageAnnotation.Tag);
    }

    [Fact]
    public void ResourceHasHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var hasHealthCheck = resource.Annotations.OfType<HealthCheckAnnotation>().Any();
        Assert.True(hasHealthCheck);
    }

    [Fact]
    public void CanAddDataVolume()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithDataVolume();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var volume = resource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(volume);
        Assert.Equal("/data", volume.Target);
        Assert.Equal(ContainerMountType.Volume, volume.Type);
    }

    [Fact]
    public void CanAddDataVolumeWithCustomName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithDataVolume("my-custom-volume");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var volume = resource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(volume);
        Assert.Equal("my-custom-volume", volume.Source);
        Assert.Equal("/data", volume.Target);
    }

    [Fact]
    public void CanAddOtelCollectorConfig()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithCollectorConfig("./otelcol-config.yaml");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var configMount = resource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(configMount);
        Assert.EndsWith("otelcol-config.yaml", configMount.Source);
        Assert.Equal("/otel-lgtm/otelcol-config.yaml", configMount.Target);
    }

    [Fact]
    public void CanAddGrafanaConfig()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithGrafanaConfig("./custom.ini");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var configMount = resource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(configMount);
        Assert.EndsWith("custom.ini", configMount.Source);
        Assert.Equal("/otel-lgtm/grafana/conf/custom.ini", configMount.Target);
    }

    [Fact]
    public void CanAddPrometheusConfig()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithPrometheusConfig("./prometheus.yaml");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<GrafanaOtelLgtmResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var configMount = resource.Annotations.OfType<ContainerMountAnnotation>().SingleOrDefault();
        Assert.NotNull(configMount);
        Assert.EndsWith("prometheus.yaml", configMount.Source);
        Assert.Equal("/otel-lgtm/prometheus.yaml", configMount.Target);
    }

    [Fact]
    [RequiresDocker]
    public async Task ContainerStartsAndHealthCheckPasses()
    {
        using var builder = TestDistributedApplicationBuilder.Create()
            .WithTestAndResourceLogging(testOutputHelper);

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm");

        using var app = builder.Build();

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await app.StartAsync();
        await resourceNotificationService.WaitForResourceHealthyAsync(lgtm.Resource.Name).WaitAsync(TimeSpan.FromMinutes(5));

        Assert.True(resourceNotificationService.TryGetCurrentState(lgtm.Resource.Name, out var resourceEvent));
        Assert.NotNull(resourceEvent);

        await app.StopAsync();
    }
}
