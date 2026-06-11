// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindContainerExtensionsTests
{
    [Fact]
    public void WithReference_Container_InjectsBindMountAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var kind = builder.AddKindCluster("test-cluster");
        var container = builder.AddContainer("test-container", "test-image")
            .WithReference(kind);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ContainerResource>());
        Assert.True(containerResource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));
        var mount = Assert.Single(mounts);
        Assert.Equal(kind.Resource.ContainerKubeconfigPath, mount.Source);
        Assert.Equal("/etc/kubeconfig/config", mount.Target);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public void WithReference_Container_InjectsEnvironmentAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var kind = builder.AddKindCluster("test-cluster");
        builder.AddContainer("test-container", "test-image")
            .WithReference(kind);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ContainerResource>());
        Assert.True(containerResource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out _));
    }

    [Fact]
    public async Task WithReference_Container_SetsContainerKubeconfigEnvironmentValue()
    {
        var builder = DistributedApplication.CreateBuilder();

        var kind = builder.AddKindCluster("test-cluster");
        var container = builder.AddContainer("test-container", "test-image")
            .WithReference(kind);

        using var app = builder.Build();

        var environment = await container.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Run);
        Assert.Equal("/etc/kubeconfig/config", environment["KUBECONFIG"]);
        Assert.Equal("test-cluster", environment["K8S_CLUSTER_NAME"]);
    }
}
