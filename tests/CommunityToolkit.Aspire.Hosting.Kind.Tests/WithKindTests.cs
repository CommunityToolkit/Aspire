// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Kubernetes;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Kind;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class WithKindTests
{
    [Fact]
    public void WithKindCreatesKindEnvironmentResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddKubernetesEnvironment("k8s").WithKind();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(model.Resources.OfType<KubernetesEnvironmentResource>());
        var kindEnv = Assert.Single(model.Resources.OfType<KindEnvironmentResource>());
        Assert.Equal("k8s-kind", kindEnv.Name);
    }

    [Fact]
    public void WithKindParentLinksToKubernetesEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddKubernetesEnvironment("k8s").WithKind();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var k8sEnv = Assert.Single(model.Resources.OfType<KubernetesEnvironmentResource>());
        var kindEnv = Assert.Single(model.Resources.OfType<KindEnvironmentResource>());
        Assert.Same(k8sEnv, kindEnv.Parent);
    }

    [Fact]
    public void FluentMethodsWorkAfterWithKind()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddKubernetesEnvironment("k8s")
            .WithKind()
            .WithKubernetesVersion("v1.32.2")
            .WithWorkerNodes(2);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var kindEnv = Assert.Single(model.Resources.OfType<KindEnvironmentResource>());
        Assert.True(kindEnv.TryGetLastAnnotation<KindNodeImageAnnotation>(out var imageAnnotation));
        Assert.Equal("v1.32.2", imageAnnotation.Version);
        Assert.True(kindEnv.TryGetLastAnnotation<WorkerNodesAnnotation>(out var workerAnnotation));
        Assert.Equal(2, workerAnnotation.Count);
    }

    [Fact]
    public void SharedFluentMethodsWorkOnBothResourceTypes()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        builder.AddKindCluster("dev")
            .WithKubernetesVersion("v1.31.0")
            .WithWorkerNodes(3);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var cluster = Assert.Single(model.Resources.OfType<KindClusterResource>());
        Assert.True(cluster.TryGetLastAnnotation<KindNodeImageAnnotation>(out var imageAnnotation));
        Assert.Equal("v1.31.0", imageAnnotation.Version);
        Assert.True(cluster.TryGetLastAnnotation<WorkerNodesAnnotation>(out var workerAnnotation));
        Assert.Equal(3, workerAnnotation.Count);
    }

    [Fact]
    public void WithClusterLifetimeWorksOnKindEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddKubernetesEnvironment("k8s")
            .WithKind()
            .WithClusterLifetime(ClusterLifetime.Persistent);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var kindEnv = Assert.Single(model.Resources.OfType<KindEnvironmentResource>());
        Assert.True(kindEnv.TryGetLastAnnotation<ClusterLifetimeAnnotation>(out var annotation));
        Assert.Equal(ClusterLifetime.Persistent, annotation.Lifetime);
    }

    [Fact]
    public void PublishAsKubernetesServiceCustomizationWorks()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddKubernetesEnvironment("k8s").WithKind();
        builder.AddContainer("redis", "redis", "7")
            .PublishAsKubernetesService(k8s =>
            {
                k8s.Service!.Spec.Type = "ClusterIP";
            });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var redis = Assert.Single(model.Resources.OfType<ContainerResource>());
        Assert.True(
            redis.TryGetAnnotationsOfType<global::Aspire.Hosting.Kubernetes.KubernetesServiceCustomizationAnnotation>(out var annotations));
        Assert.Single(annotations);
    }

    [Fact]
    public void WithKindThrowsOnNull()
    {
        IResourceBuilder<KubernetesEnvironmentResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithKind());
    }

    [Fact]
    public void WithKindIsInvisibleInRunMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        builder.AddKubernetesEnvironment("k8s").WithKind();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Empty(model.Resources.OfType<KindEnvironmentResource>());
        Assert.Empty(model.Resources.OfType<KubernetesEnvironmentResource>());
    }
}
