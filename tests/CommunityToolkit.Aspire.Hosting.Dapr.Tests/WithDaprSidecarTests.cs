// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

public class WithDaprSidecarTests
{
    [Fact]
    public void ParentResourceConfiguredWithSidecarAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rb = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("test")
            .WithDaprSidecar();

        Assert.Single(rb.Resource.Annotations.OfType<DaprSidecarAnnotation>());
    }

    [Fact]
    public void ResourceAddedWithHiddenInitialState()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rb = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("test")
            .WithDaprSidecar();

        var resource = Assert.Single(builder.Resources.OfType<DaprSidecarResource>());
        Assert.True(resource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.True(annotation.InitialSnapshot.IsHidden);
    }

    [Fact]
    public void OptionsCanBeConfiguredOnSidecar()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("test")
            .WithDaprSidecar(new DaprSidecarOptions { AppId = "appId" });

        var resource = Assert.Single(builder.Resources.OfType<DaprSidecarResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<DaprSidecarOptionsAnnotation>());

        Assert.Equal("appId", annotation.Options.AppId);
    }

    [Fact]
    public void OptionsCanBeConfiguredUsingCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("test")
            .WithDaprSidecar(b => b.WithOptions(new DaprSidecarOptions { AppId = "appId" }));

        var resource = Assert.Single(builder.Resources.OfType<DaprSidecarResource>());
        var annotation = Assert.Single(resource.Annotations.OfType<DaprSidecarOptionsAnnotation>());

        Assert.Equal("appId", annotation.Options.AppId);
    }

    [Fact]
    public void DaprSidecarSupportsWaitFor()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("service-a")
            .WithDaprSidecar();

        var sidecarResource = Assert.Single(builder.Resources.OfType<IDaprSidecarResource>());

        // Verify that IDaprSidecarResource implements IResourceWithWaitSupport
        Assert.IsAssignableFrom<IResourceWithWaitSupport>(sidecarResource);

        // Create a resource builder and use it with WaitFor
        var sidecarResourceBuilder = builder.CreateResourceBuilder(sidecarResource);

        var serviceB = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceB>("service-b")
            .WaitFor(sidecarResourceBuilder);

        // Verify the wait annotation was added
        Assert.NotNull(serviceB);
        var waitAnnotation = Assert.Single(serviceB.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Equal(sidecarResource.Name, waitAnnotation.Resource.Name);
    }

    [Fact]
    public void DaprSidecarCanReferenceComponents()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var stateStore = builder.AddDaprStateStore("statestore");
        var pubSub = builder.AddDaprPubSub("pubsub");
        
        builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("test")
            .WithDaprSidecar(sidecar => 
            {
                sidecar.WithReference(stateStore).WithReference(pubSub);
            });
        
        var sidecarResource = Assert.Single(builder.Resources.OfType<DaprSidecarResource>());
        
        // Verify that component references are correctly added to the sidecar
        var referenceAnnotations = sidecarResource.Annotations.OfType<DaprComponentReferenceAnnotation>().ToList();
        Assert.Equal(2, referenceAnnotations.Count);
        
        // Verify specific component references
        Assert.Contains(referenceAnnotations, a => a.Component.Name == "statestore");
        Assert.Contains(referenceAnnotations, a => a.Component.Name == "pubsub");
    }

#pragma warning disable CS0618 // Type or member is obsolete
    [Fact]
    public void DaprSidecarCanReferenceComponentsUsingDeprecatedAPI()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var stateStore = builder.AddDaprStateStore("statestore");
        var pubSub = builder.AddDaprPubSub("pubsub");
        
        var projectBuilder = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Dapr_ServiceA>("test")
            .WithDaprSidecar();
        
        // Use the deprecated API that adds component references to the project resource
        projectBuilder.WithReference(stateStore).WithReference(pubSub);
        
        var projectResource = projectBuilder.Resource;
        
        // Verify that component references are added to the project resource (deprecated behavior)
        var projectReferenceAnnotations = projectResource.Annotations.OfType<DaprComponentReferenceAnnotation>().ToList();
        Assert.Equal(2, projectReferenceAnnotations.Count);
        
        // Verify specific component references
        Assert.Contains(projectReferenceAnnotations, a => a.Component.Name == "statestore");
        Assert.Contains(projectReferenceAnnotations, a => a.Component.Name == "pubsub");
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
