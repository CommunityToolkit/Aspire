// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using k8s;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindHelmChartTests
{
    [Fact]
    public void AddHelmChartCreatesResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
    public void WithHelmValuesFileAddsPath()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
        using var builder = TestDistributedApplicationBuilder.Create();

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
    public async Task InstallAsync_PublishesCrdsNewlyObservedAfterSuccessfulInstall()
    {
        var resource = new KindHelmChartResource("redis", "chart/ref", new KindClusterResource("cluster"));
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/existing.example.com", ""));
        runner.Results.Enqueue(new(0, "release installed", ""));
        runner.Results.Enqueue(new(0, """
            customresourcedefinition.apiextensions.k8s.io/existing.example.com
            customresourcedefinition.apiextensions.k8s.io/widgets.example.com
            """, ""));
        using var loggerFactory = LoggerFactory.Create(_ => { });

        await new HelmManager(runner).InstallAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);

        var crdNames = KindDeploymentOutcomes.GetOrCreate(resource).CrdNames;
        Assert.Equal(
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"],
            crdNames);
        Assert.Equal(["kubectl", "helm", "kubectl"], runner.Commands.Select(command => command.FileName));
    }

    [Fact]
    public async Task InstallAsync_FailsBeforeHelmWhenCrdBaselineCannotBeQueried()
    {
        var resource = new KindHelmChartResource("redis", "chart/ref", new KindClusterResource("cluster"));
        var runner = new FakeProcessRunner { NextResult = new(1, "", "forbidden") };
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HelmManager(runner).InstallAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken));

        Assert.Contains("Failed to query custom resource definitions", error.Message);
        Assert.DoesNotContain(runner.Commands, command => command.FileName == "helm");
        Assert.False(resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out _));
    }

    [Fact]
    public async Task InstallAsync_DoesNotDiscoverCrdsAfterFailedHelmInstall()
    {
        var resource = new KindHelmChartResource("redis", "chart/ref", new KindClusterResource("cluster"));
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "", ""));
        runner.Results.Enqueue(new(1, "", "release failed"));
        using var loggerFactory = LoggerFactory.Create(_ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HelmManager(runner).InstallAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken));

        Assert.False(resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out _));
        Assert.Equal(["kubectl", "helm"], runner.Commands.Select(command => command.FileName));
    }

    [Fact]
    public async Task InstallAsync_FailedInstallRetainsLastSuccessfulOutcome()
    {
        var resource = new KindHelmChartResource("redis", "chart/ref", new KindClusterResource("cluster"));
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "", ""));
        runner.Results.Enqueue(new(0, "release installed", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        runner.Results.Enqueue(new(1, "", "installation failed"));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new HelmManager(runner);

        await manager.InstallAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        var crdNames = KindDeploymentOutcomes.GetOrCreate(resource).CrdNames;
        Assert.Equal(["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"], crdNames);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.InstallAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken));
        Assert.Same(crdNames, KindDeploymentOutcomes.GetOrCreate(resource).CrdNames);
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
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");
        string name = null!;

        var action = () => cluster.AddHelmChart(name, "chart/ref");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddHelmChartShouldThrowWhenChartRefIsNull()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
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
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var helmResource = Assert.Single(appModel.Resources.OfType<KindHelmChartResource>());
        var healthCheckAnnotations = helmResource.Annotations.OfType<HealthCheckAnnotation>();
        Assert.Contains(healthCheckAnnotations, annotation => annotation.Key == "helm_redis");
    }

    [Fact]
    public void AddHelmChartKeepsOnlyWorkloadHealthCheck()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster").AddHelmChart("redis", "chart/ref").Resource;

        using var app = builder.Build();

        Assert.Equal("helm_redis", Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>()).Key);
    }

    [Fact]
    public async Task HelmPostApplyCheckWaitsForEveryNewCrd()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/existing.example.com", ""));
        runner.Results.Enqueue(new(0, "release installed", ""));
        runner.Results.Enqueue(new(0, """
            customresourcedefinition.apiextensions.k8s.io/existing.example.com
            customresourcedefinition.apiextensions.k8s.io/widgets.example.com
            customresourcedefinition.apiextensions.k8s.io/gadgets.example.com
            """, ""));
        var kubernetes = FakeCrdClient.Established("widgets.example.com", "gadgets.example.com");
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster").AddHelmChart("redis", "chart/ref").Resource;
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });

        await new HelmManager(runner).InstallAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);

        Assert.Equal(3, runner.Commands.Count);
        Assert.Equal(["gadgets.example.com", "widgets.example.com"], kubernetes.ReadNames.Order());
        Assert.All(runner.Commands.Where(command => command.FileName == "kubectl"),
            command => Assert.StartsWith("get crd ", command.Arguments));
    }

    [Fact]
    public async Task HelmBestEffortCrdReadFailureAllowsPostApplyToComplete()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "", ""));
        runner.Results.Enqueue(new(0, "release installed", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient((_, _) =>
            Task.FromException<k8s.Models.V1CustomResourceDefinition>(new HttpRequestException("connection refused")));
        using var builder = TestDistributedApplicationBuilder.Create();
        var chart = builder.AddKindCluster("test-cluster").AddHelmChart("redis", "chart/ref");
        Assert.Same(chart, chart.WithCrdWaitBehavior(CrdWaitBehavior.BestEffort));
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });

        await new HelmManager(runner).InstallAsync(chart.Resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(chart.Resource, TestContext.Current.CancellationToken);

        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
        Assert.Equal(3, runner.Commands.Count);
    }

    [Fact]
    public void WithCrdWaitTimeoutConfiguresHelmCrdPolicy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var chart = builder.AddKindCluster("test-cluster").AddHelmChart("redis", "chart/ref");

        Assert.Same(chart, chart.WithCrdWaitTimeout(TimeSpan.FromMilliseconds(500)));
        Assert.True(chart.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.Equal(TimeSpan.FromSeconds(1), policy.Timeout);
    }

    [Fact]
    public async Task GetCrdSnapshotStopsHungKubectlProbe()
    {
        var runner = new FakeProcessRunner();
        runner.Delays.Enqueue(TimeSpan.FromHours(1));
        var manager = new KubectlManager(runner, apiProbeTimeout: TimeSpan.FromMilliseconds(50));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            manager.GetCustomResourceDefinitionsAsync(
                new KindClusterResource("cluster").KubeconfigPath, loggerFactory.CreateLogger("test"), cts.Token));
    }

    [Fact]
    public async Task HelmCrdFailureFailsToStartWithoutRunning()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "", ""));
        runner.Results.Enqueue(new(0, "release installed", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient((_, _) => Task.FromException<k8s.Models.V1CustomResourceDefinition>(
            new InvalidOperationException("CRD read failed")));
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        var cluster = builder.AddResource(new KindClusterResource("test-cluster"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Kind Cluster",
                State = KnownResourceStates.Running,
                Properties = [],
            });
        cluster.AddHelmChart("redis", "chart/ref");
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(8));

        var startTask = app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync("redis", KnownResourceStates.FailedToStart, cts.Token);
        await startTask;

        Assert.True(notifications.TryGetCurrentState("redis", out var current));
        Assert.Equal(KnownResourceStates.FailedToStart, current.Snapshot.State?.Text);
        Assert.Equal(3, runner.Commands.Count);
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
    }

    [Fact]
    public void AddHelmChartRegistersUniqueHealthCheckPerResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

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