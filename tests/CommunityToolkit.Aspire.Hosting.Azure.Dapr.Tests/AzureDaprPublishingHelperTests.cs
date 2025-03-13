using Aspire.Hosting.Utils;
using Aspire.Hosting;
using StackExchange.Redis;
using Aspire.Hosting.Azure;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr.Tests;
public class AzureDaprPublishingHelperTests
{
    [Fact]
    public async Task ExecuteProviderSpecificRequirements_AddsAzureContainerAppCustomizationAnnotation_WhenPublishAsAzureContainerAppIsUsed()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureRedis("redisState").RunAsContainer();

        var daprState = builder.AddDaprStateStore("daprState");

        builder.AddContainer("name", "image")
                .PublishAsAzureContainerApp((infrastructure, container) => { })
                .WithReference(daprState)
                .WithDaprSidecar();

        using var app = builder.Build();

       await ExecuteBeforeStartHooksAsync(app, default);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.GetContainerResources());

        Assert.Equal(2, containerResource.Annotations.OfType<AzureContainerAppCustomizationAnnotation>().Count());
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}
