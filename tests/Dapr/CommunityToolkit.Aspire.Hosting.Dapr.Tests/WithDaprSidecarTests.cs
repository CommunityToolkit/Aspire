// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

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

        Assert.Equal(KnownResourceStates.Hidden, annotation.InitialSnapshot.State?.Text);
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
}
