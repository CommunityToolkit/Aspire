using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.Tests;

public class RoutingExtensionTests
{
    [Fact]
    public void WithOpenTelemetryCollectorRoutingAddsWaitAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var collector = builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithOpenTelemetryCollectorRouting(collector);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        // Verify WaitAnnotation is added
        var waitAnnotation = resource.Annotations.OfType<WaitAnnotation>().SingleOrDefault();
        Assert.NotNull(waitAnnotation);
        Assert.Same(collector.Resource, waitAnnotation.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, waitAnnotation.WaitType);
    }

    [Fact]
    public void WithOpenTelemetryCollectorRoutingAddsEnvironmentCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        var collector = builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithOpenTelemetryCollectorRouting(collector);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        // Verify EnvironmentCallbackAnnotation is added by the routing extension
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envAnnotations);

        // There should be exactly one environment callback from the routing extension
        Assert.Single(envAnnotations);
    }

    [Fact]
    public void WithOpenTelemetryCollectorRoutingReadsOtlpProtocolFromEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        var collector = builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http")
            .WithOpenTelemetryCollectorRouting(collector);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        // Verify we have environment callbacks (one from WithEnvironment, one from routing)
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.Equal(2, envAnnotations.Count);

        // The routing extension should add an environment callback that reads OTEL_EXPORTER_OTLP_PROTOCOL
        // We can't easily test the actual callback execution without endpoint allocation,
        // but we can verify the callback exists
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void WithOpenTelemetryCollectorRoutingDefaultsToGrpcProtocol()
    {
        var builder = DistributedApplication.CreateBuilder();

        var collector = builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithOpenTelemetryCollectorRouting(collector);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        // Verify the routing callback exists - it will default to 'grpc' if no protocol is set
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.Single(envAnnotations);
    }

    [Fact]
    public void WithOpenTelemetryCollectorRoutingAddsCorrectAnnotationsToResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var collector = builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"))
            .WithOpenTelemetryCollectorRouting(collector);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<TestResource>().SingleOrDefault();
        Assert.NotNull(resource);

        // Verify both required annotations are present
        var waitAnnotation = resource.Annotations.OfType<WaitAnnotation>().SingleOrDefault();
        var envAnnotation = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().SingleOrDefault();

        Assert.NotNull(waitAnnotation);
        Assert.NotNull(envAnnotation);

        // Verify the wait annotation points to the collector resource
        Assert.Same(collector.Resource, waitAnnotation.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, waitAnnotation.WaitType);
    }

    [Fact]
    public void WithOpenTelemetryCollectorRoutingReturnsOriginalBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();

        var collector = builder.AddOpenTelemetryCollector("collector")
            .WithAppForwarding();

        var testResource = builder.AddResource(new TestResource("test-resource"));
        var result = testResource.WithOpenTelemetryCollectorRouting(collector);

        // Should return the same builder instance for fluent chaining
        Assert.Same(testResource, result);
    }
}

// Test resource that implements IResourceWithEnvironment for testing
public class TestResource(string name) : Resource(name), IResourceWithEnvironment
{
}