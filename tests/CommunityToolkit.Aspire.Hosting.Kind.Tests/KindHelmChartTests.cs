// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

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
    public void WithHelmStringValueAddsStringValue()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithHelmStringValue("auth.password", "000123")
            .WithHelmStringValue("feature.flag", "false");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal(2, resource.StringValues.Count);
        Assert.Equal("000123", resource.StringValues["auth.password"]);
        Assert.Equal("false", resource.StringValues["feature.flag"]);
    }

    [Fact]
    public void WithHelmValueLastWriteWinsForDuplicateKey()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithHelmValue("replica.replicaCount", "1")
            .WithHelmValue("replica.replicaCount", "2");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal("2", resource.Values["replica.replicaCount"]);
    }

    [Fact]
    public void WithHelmValueAndStringValueUseLastWriteWinsAcrossModes()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithHelmValue("auth.password", "123")
            .WithHelmStringValue("auth.password", "000123");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.False(resource.Values.ContainsKey("auth.password"));
        Assert.Equal("000123", resource.StringValues["auth.password"]);
    }

    [Fact]
    public void WithHelmStringValueAndValueUseLastWriteWinsAcrossModes()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithHelmStringValue("auth.password", "000123")
            .WithHelmValue("auth.password", "123");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.False(resource.StringValues.ContainsKey("auth.password"));
        Assert.Equal("123", resource.Values["auth.password"]);
    }

    [Fact]
    public void WithCrdWaitRetrySetsRetryConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithCrdWaitRetry(maxAttempts: 3, backoff: TimeSpan.FromSeconds(7));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal(3, resource.CrdWaitRetryMaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(7), resource.CrdWaitRetryBackoff);
    }

    [Fact]
    public void WithCrdWaitRetryUsesDefaultBackoff()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
            .WithCrdWaitRetry();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal(3, resource.CrdWaitRetryMaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(5), resource.CrdWaitRetryBackoff);
    }

    [Fact]
    public void WithCrdWaitRetryRejectsLessThanTwoAttempts()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddHelmChart("redis", "chart/ref").WithCrdWaitRetry(1, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WithCrdWaitRetryRejectsInvalidBackoff()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddHelmChart("redis", "chart/ref").WithCrdWaitRetry(3, TimeSpan.Zero));
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
        Assert.Empty(resource.StringValues);
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
            .WithHelmStringValue("auth.password", "000123")
            .WithHelmValuesFile("./values.yaml")
            .WithNamespace("cache");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        Assert.Equal("20.0.0", resource.Version);
        Assert.Equal("2", resource.Values["replica.replicaCount"]);
        Assert.Equal("000123", resource.StringValues["auth.password"]);
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
        resource.StringValues["auth.password"] = "000123";
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
            "--set-string",
            "auth.password=000123",
            "-f",
            @"C:\temp path\values file.yaml",
        ],
        arguments);
    }

    [Fact]
    public async Task InstallAsync_RetriesAfterWaitingForNewCrds()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new KindHelmChartResource("redis", "chart/ref", cluster)
        {
            CrdWaitRetryMaxAttempts = 3,
            CrdWaitRetryBackoff = TimeSpan.FromSeconds(2),
        };
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "", "")); // kubectl get crd baseline
        processRunner.Results.Enqueue(new(1, "", "no matches for kind \"Widget\" in version \"widgets.example.com/v1\"; ensure CRDs are installed first"));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", "")); // kubectl get crd after failure
        processRunner.Results.Enqueue(new(0, "", "")); // kubectl wait
        processRunner.Results.Enqueue(new(0, "release installed", "")); // retry succeeds
        var delays = new List<TimeSpan>();
        var manager = new HelmManager(
            processRunner,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        using var loggerFactory = LoggerFactory.Create(_ => { });

        await manager.InstallAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None);

        Assert.Equal(5, processRunner.Commands.Count);
        Assert.Equal("kubectl", processRunner.Commands[0].FileName);
        Assert.Contains("get crd -o name", processRunner.Commands[0].Arguments);
        Assert.Equal("helm", processRunner.Commands[1].FileName);
        Assert.Equal("kubectl", processRunner.Commands[2].FileName);
        Assert.Contains("get crd -o name", processRunner.Commands[2].Arguments);
        Assert.Equal("kubectl", processRunner.Commands[3].FileName);
        Assert.Contains("wait --for=condition=Established customresourcedefinition.apiextensions.k8s.io/widgets.example.com", processRunner.Commands[3].Arguments);
        Assert.Equal("helm", processRunner.Commands[4].FileName);
        Assert.Equal([TimeSpan.FromSeconds(2)], delays);
    }

    [Fact]
    public async Task InstallAsync_DoesNotRetryWithoutNewCrds()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new KindHelmChartResource("redis", "chart/ref", cluster)
        {
            CrdWaitRetryMaxAttempts = 3,
            CrdWaitRetryBackoff = TimeSpan.FromSeconds(2),
        };
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "", "")); // kubectl get crd baseline
        processRunner.Results.Enqueue(new(1, "", "no matches for kind \"Widget\" in version \"widgets.example.com/v1\"; ensure CRDs are installed first"));
        processRunner.Results.Enqueue(new(0, "", "")); // kubectl get crd after failure
        var manager = new HelmManager(processRunner, static (_, _) => Task.CompletedTask);
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.InstallAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None));

        Assert.Contains("Failed to install Helm chart", ex.Message);
        Assert.Equal(3, processRunner.Commands.Count);
    }

    [Fact]
    public void ComputeRetryBackoffDoublesPerFailure()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), HelmManager.ComputeRetryBackoff(TimeSpan.FromSeconds(5), 1));
        Assert.Equal(TimeSpan.FromSeconds(10), HelmManager.ComputeRetryBackoff(TimeSpan.FromSeconds(5), 2));
        Assert.Equal(TimeSpan.FromSeconds(20), HelmManager.ComputeRetryBackoff(TimeSpan.FromSeconds(5), 3));
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
    public void WithHelmStringValueShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindHelmChartResource> builder = null!;

        var action = () => builder.WithHelmStringValue("key", "value");

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
    public void WithCrdWaitRetryShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<KindHelmChartResource> builder = null!;

        var action = () => builder.WithCrdWaitRetry();

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