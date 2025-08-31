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
}
