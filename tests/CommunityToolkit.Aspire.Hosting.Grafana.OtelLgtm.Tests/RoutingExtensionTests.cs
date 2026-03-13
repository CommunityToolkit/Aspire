using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Grafana.OtelLgtm.Tests;

public class RoutingExtensionTests
{
    [Fact]
    public void WithGrafanaOtelLgtmRoutingAddsWaitAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithGrafanaOtelLgtmRouting(lgtm);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var waitAnnotation = resource.Annotations.OfType<WaitAnnotation>().SingleOrDefault();
        Assert.NotNull(waitAnnotation);
        Assert.Same(lgtm.Resource, waitAnnotation.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, waitAnnotation.WaitType);
    }

    [Fact]
    public void WithGrafanaOtelLgtmRoutingAddsEnvironmentCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithGrafanaOtelLgtmRouting(lgtm);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envAnnotations);
        Assert.Single(envAnnotations);
    }

    [Fact]
    public void WithGrafanaOtelLgtmRoutingReadsOtlpProtocolFromEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http")
            .WithGrafanaOtelLgtmRouting(lgtm);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.Equal(2, envAnnotations.Count);
    }

    [Fact]
    public void WithGrafanaOtelLgtmRoutingDefaultsToGrpcProtocol()
    {
        var builder = DistributedApplication.CreateBuilder();

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithGrafanaOtelLgtmRouting(lgtm);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.Single(envAnnotations);
    }

    [Fact]
    public void WithGrafanaOtelLgtmRoutingAddsCorrectAnnotationsToResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithGrafanaOtelLgtmRouting(lgtm);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        var waitAnnotation = resource.Annotations.OfType<WaitAnnotation>().SingleOrDefault();
        var envAnnotation = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().SingleOrDefault();

        Assert.NotNull(waitAnnotation);
        Assert.NotNull(envAnnotation);

        Assert.Same(lgtm.Resource, waitAnnotation.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, waitAnnotation.WaitType);
    }

    [Fact]
    public void WithGrafanaOtelLgtmRoutingReturnsOriginalBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"));
        var result = testResource.WithGrafanaOtelLgtmRouting(lgtm);

        Assert.Same(testResource, result);
    }

    [Fact]
    public void WithAppForwardingReturnsOriginalBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();

        var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm");
        var result = lgtm.WithAppForwarding();

        Assert.Same(lgtm, result);
    }
}

public class TestResource(string name) : Resource(name), IResourceWithEnvironment
{
}
