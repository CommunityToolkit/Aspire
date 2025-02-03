// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

public class AddDaprStateStoreTests
{
    [Fact]
    public void DistributedApplicationBuilderCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddDaprStateStore(null!));
    }

    [Fact]
    public void ResourceNameCannotBeOmitted()
    {
        string name = "";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddDaprStateStore(name));

        name = " ";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddDaprStateStore(name));

        name = null!;
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddDaprStateStore(name));
    }

    [Fact]
    public void OptionsConfiguredOnDaprComponent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = "statestore";
        var type = DaprConstants.BuildingBlocks.StateStore;
        var options = new DaprComponentOptions { LocalPath = "path" };

        builder.AddDaprStateStore(name, options);

        var resource = builder.Resources.Single();
        var daprResource = Assert.IsType<DaprComponentResource>(resource);
        Assert.Equal(name, resource.Name);
        Assert.Equal(type, daprResource.Type);
        Assert.Equal(options, daprResource.Options);
    }

    [Fact]
    public void ResourceConfiguredWithHiddenIntialState()
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = "statestore";

        builder.AddDaprStateStore(name);

        var resource = builder.Resources.Single();
        var daprResource = Assert.IsType<DaprComponentResource>(resource);

        Assert.True(daprResource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Hidden, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public void ResourceIncludedInManifest()
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = "statestore";

        builder.AddDaprStateStore(name);

        var resource = builder.Resources.Single();
        var daprResource = Assert.IsType<DaprComponentResource>(resource);

        Assert.True(daprResource.TryGetAnnotationsOfType<ManifestPublishingCallbackAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.NotNull(annotation.Callback);
    }
}