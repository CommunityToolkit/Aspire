// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class AddKindClusterTests
{
    [Fact]
    public void AddKindClusterCreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        Assert.Equal("test-cluster", resource.Name);
    }

    [Fact]
    public void WithKubernetesVersionSetsVersion()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster")
            .WithKubernetesVersion("v1.32.2");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        Assert.Equal("v1.32.2", resource.KubernetesVersion);
    }

    [Fact]
    public void WithWorkerNodesSetsCount()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster")
            .WithWorkerNodes(3);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        Assert.Equal(3, resource.WorkerNodes);
    }

    [Fact]
    public void DefaultsAreCorrect()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        Assert.Equal(0, resource.WorkerNodes);
        Assert.Null(resource.KubernetesVersion);
    }

    [Fact]
    public void WithWorkerNodesRejectsNegative()
    {
        var builder = DistributedApplication.CreateBuilder();
        var resourceBuilder = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() => resourceBuilder.WithWorkerNodes(-1));
    }

    [Fact]
    public void AddKindClusterThrowsOnNullBuilder()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddKindCluster("test"));
    }

    [Fact]
    public void AddKindClusterThrowsOnNullName()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddKindCluster(null!));
    }

    [Fact]
    public void GeneratedConfigContainsImageFromKindContainerImageTags()
    {
        var resource = new KindClusterResource("test-cluster")
        {
            KubernetesVersion = KindContainerImageTags.DefaultKubernetesVersion,
        };

        var configPath = KindConfigGenerator.GenerateConfig(resource);

        try
        {
            var yaml = File.ReadAllText(configPath);
            var expectedImage = $"{KindContainerImageTags.KindNodeImageRepository}:{KindContainerImageTags.DefaultKubernetesVersion}";
            Assert.Contains(expectedImage, yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public void WithReferenceInjectsEnvironmentAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var kind = builder.AddKindCluster("test-cluster");
        builder.AddResource(new TestResource("svc"))
            .WithReference(kind);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var svc = Assert.Single(appModel.Resources.OfType<TestResource>());
        Assert.True(svc.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out _));
    }

    [Fact]
    public void DefaultClusterLifetimeIsSession()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        // No annotation means default Session lifetime
        Assert.False(resource.TryGetLastAnnotation<ClusterLifetimeAnnotation>(out _));
    }

    [Fact]
    public void WithClusterLifetimeSetsAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster")
            .WithClusterLifetime(ClusterLifetime.Persistent);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        Assert.True(resource.TryGetLastAnnotation<ClusterLifetimeAnnotation>(out var annotation));
        Assert.Equal(ClusterLifetime.Persistent, annotation.Lifetime);
    }

    [Fact]
    public void AddKindClusterRegistersLifecycleHook()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster");

        // Verify the eventing subscriber is registered in DI
        var descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IDistributedApplicationEventingSubscriber) &&
                 d.ImplementationType == typeof(KindClusterLifecycleHook));

        Assert.NotNull(descriptor);
    }

    private sealed class TestResource(string name) : Resource(name), IResourceWithEnvironment;
}
