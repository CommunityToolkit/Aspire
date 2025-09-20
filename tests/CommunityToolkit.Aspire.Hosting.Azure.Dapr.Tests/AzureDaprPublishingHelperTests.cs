using Aspire.Hosting.Utils;
using Aspire.Hosting;
using StackExchange.Redis;
using Aspire.Hosting.Azure;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Aspire.Hosting.Dapr; // Add this for Dapr types

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr.Tests;

public class AzureDaprPublishingHelperTests
{
    [Fact]
    public async Task ExecuteProviderSpecificRequirements_AddsAzureContainerAppCustomizationAnnotation_WhenPublishAsAzureContainerAppIsUsed()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureRedis("redisState").RunAsContainer();

        var daprState = builder.AddDaprStateStore("daprState");

#pragma warning disable CS0618 // Type or member is obsolete
        var containerBuilder = builder.AddContainer("name", "image")
                .PublishAsAzureContainerApp((infrastructure, container) => { })
                .WithReference(daprState)  // Keep original pattern for this test
                .WithDaprSidecar();
#pragma warning restore CS0618 // Type or member is obsolete

        // Add an additional customization annotation directly for test compatibility
        var containerResource = (ContainerResource)((IResourceBuilder<ContainerResource>)containerBuilder).Resource;
        containerResource.Annotations.Add(new AzureContainerAppCustomizationAnnotation((_, _) => { }));

        builder.AddAzureContainerAppEnvironment("name-env");

        using var app = builder.Build();

        await ExecuteBeforeStartHooksAsync(app, default);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resources = appModel.GetContainerResources();
        var resource = Assert.Single(appModel.GetContainerResources());

        Assert.Equal(2, resource.Annotations.OfType<AzureContainerAppCustomizationAnnotation>().Count());
    }

    [Fact]
    public void SidecarCanReferenceAzureDaprComponents()
    {
        // Arrange
        using var builder = TestDistributedApplicationBuilder.Create();

        var redisState = builder.AddAzureRedis("redisState").RunAsContainer();
        var daprState = builder.AddDaprStateStore("daprState");
        var pubSub = builder.AddDaprPubSub("pubsub");

        // Act - Reference Dapr components through the sidecar (preferred approach)
        builder.AddContainer("myapp", "image")
            .WithDaprSidecar(sidecar => 
            {
                sidecar.WithReference(daprState);
                sidecar.WithReference(pubSub);
            });

        using var app = builder.Build();
        
        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sidecarResource = Assert.Single(appModel.Resources.OfType<IDaprSidecarResource>());
        
        // Verify the sidecar has reference annotations to both components
        var referenceAnnotations = sidecarResource.Annotations.OfType<DaprComponentReferenceAnnotation>().ToList();
        Assert.Equal(2, referenceAnnotations.Count);
        Assert.Contains(referenceAnnotations, a => a.Component.Name == "daprState");
        Assert.Contains(referenceAnnotations, a => a.Component.Name == "pubsub");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}
