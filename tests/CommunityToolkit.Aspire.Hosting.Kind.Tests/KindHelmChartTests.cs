// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindHelmChartTests
{
    [Fact]
    public void AddHelmChartCreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal("redis", resource.Name);
        Assert.Equal("oci://registry-1.docker.io/bitnamicharts/redis", resource.ChartRef);
    }

    [Fact]
    public void AddHelmChartSetsParent()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var helmResource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        var clusterResource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        Assert.Same(clusterResource, helmResource.Parent);
    }

    [Fact]
    public void ReleaseNameDefaultsToResourceName()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new KindHelmChartResource("my-release", "chart/ref", cluster);

        Assert.Equal("my-release", resource.ReleaseName);
    }

    [Fact]
    public void WithChartVersionSetsVersion()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithChartVersion("20.0.0");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal("20.0.0", resource.Version);
    }

    [Fact]
    public void WithHelmValueAddsValue()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithHelmValue("replica.replicaCount", "2")
            .WithHelmValue("auth.enabled", "false");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal(2, resource.Values.Count);
        Assert.Equal("2", resource.Values["replica.replicaCount"]);
        Assert.Equal("false", resource.Values["auth.enabled"]);
    }

    [Fact]
    public void WithHelmValuesFileAddsPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithHelmValuesFile("./values/redis.yaml")
            .WithHelmValuesFile("./values/overrides.yaml");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal(2, resource.ValuesFiles.Count);
        Assert.Contains("./values/redis.yaml", resource.ValuesFiles);
        Assert.Contains("./values/overrides.yaml", resource.ValuesFiles);
    }

    [Fact]
    public void WithNamespaceSetsNamespace()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithNamespace("cache");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal("cache", resource.Namespace);
    }

    [Fact]
    public void DefaultNamespaceIsNull()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new KindHelmChartResource("redis", "chart/ref", cluster);

        Assert.Null(resource.Namespace);
    }

    [Fact]
    public void DefaultVersionIsNull()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new KindHelmChartResource("redis", "chart/ref", cluster);

        Assert.Null(resource.Version);
    }

    [Fact]
    public void ValuesAndValuesFilesStartEmpty()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new KindHelmChartResource("redis", "chart/ref", cluster);

        Assert.Empty(resource.Values);
        Assert.Empty(resource.ValuesFiles);
    }

    [Fact]
    public void MultipleHelmChartsCanBeAddedToSameCluster()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis");
        cluster.AddHelmChart("prometheus", "prometheus-community/kube-prometheus-stack");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var helmResources = appModel.Resources.OfType<KindHelmChartResource>().ToList();
        Assert.Equal(2, helmResources.Count);
        Assert.All(helmResources, r => Assert.Same(cluster.Resource, r.Parent));
    }

    [Fact]
    public void FluentApiChainingWorks()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithChartVersion("20.0.0")
            .WithHelmValue("replica.replicaCount", "2")
            .WithHelmValuesFile("./values.yaml")
            .WithNamespace("cache");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal("20.0.0", resource.Version);
        Assert.Equal("2", resource.Values["replica.replicaCount"]);
        Assert.Single(resource.ValuesFiles);
        Assert.Equal("cache", resource.Namespace);
    }

    [Fact]
    public void CreateInstallArgumentsPreservesArgumentBoundaries()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new KindHelmChartResource("redis", "./charts/my chart", cluster)
        {
            Version = "20.0.0",
            Namespace = "cache",
        };

        resource.Values["annotations.description"] = "My \"Redis\" App";
        resource.ValuesFiles.Add(@"C:\temp path\values file.yaml");

        var arguments = HelmManager.CreateInstallArguments(resource);

        Assert.Equal(
        [
            "upgrade",
            "--install",
            "redis",
            "./charts/my chart",
            $"--kubeconfig={cluster.KubeconfigPath}",
            "--version",
            "20.0.0",
            "--namespace",
            "cache",
            "--create-namespace",
            "--set",
            "annotations.description=My \"Redis\" App",
            "-f",
            @"C:\temp path\values file.yaml",
        ],
        arguments);
    }

    // ── Null-check tests ─────────────────────────────────────────────────

    [Fact]
    public void AddHelmChartShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindClusterResource> builder = null!;

        var action = () => builder.AddHelmChart("redis", "chart/ref");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddHelmChartShouldThrowWhenNameIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");
        string name = null!;

        var action = () => cluster.AddHelmChart(name, "chart/ref");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddHelmChartShouldThrowWhenChartRefIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");
        string chartRef = null!;

        var action = () => cluster.AddHelmChart("redis", chartRef);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(chartRef), exception.ParamName);
    }

    [Fact]
    public void WithChartVersionShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindHelmChartResource> builder = null!;

        var action = () => builder.WithChartVersion("1.0.0");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithHelmValueShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindHelmChartResource> builder = null!;

        var action = () => builder.WithHelmValue("key", "value");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithHelmValuesFileShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindHelmChartResource> builder = null!;

        var action = () => builder.WithHelmValuesFile("./values.yaml");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithNamespaceShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindHelmChartResource> builder = null!;

        var action = () => builder.WithNamespace("default");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void KindHelmChartResourceShouldThrowWhenParentIsNull()
    {
        KindClusterResource parent = null!;

        var action = () => new KindHelmChartResource("redis", "chart/ref", parent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(parent), exception.ParamName);
    }

    [Fact]
    public void KindHelmChartResourceShouldThrowWhenChartRefIsNull()
    {
        var cluster = new KindClusterResource("cluster");
        string chartRef = null!;

        var action = () => new KindHelmChartResource("redis", chartRef, cluster);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(chartRef), exception.ParamName);
    }

    [Fact]
    public void AddHelmChartRegistersHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var helmResource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        var healthCheckAnnotations = helmResource.Annotations.OfType<HealthCheckAnnotation>();
        Assert.NotEmpty(healthCheckAnnotations);
    }

    [Fact]
    public void AddHelmChartRegistersUniqueHealthCheckPerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis");
        cluster.AddHelmChart("prometheus", "prometheus-community/kube-prometheus-stack");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var helmResources = appModel.Resources.OfType<KindHelmChartResource>().ToList();
        Assert.Equal(2, helmResources.Count);

        foreach (KindHelmChartResource resource in helmResources)
        {
            var healthCheckAnnotations = resource.Annotations.OfType<HealthCheckAnnotation>();
            Assert.NotEmpty(healthCheckAnnotations);
        }
    }
}
