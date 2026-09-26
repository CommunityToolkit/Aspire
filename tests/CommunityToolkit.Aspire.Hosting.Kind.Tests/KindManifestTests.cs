// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;
using k8s;
using k8s.Autorest;
using k8s.Models;
using System.Net;
using System.ComponentModel;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindManifestTests
{
    [Fact]
    public void AddManifestCreatesResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        var absolutePath = Path.Combine(AppContext.BaseDirectory, "manifests", "crds.yaml");
        cluster.AddManifest("crds", absolutePath);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal("crds", resource.Name);
        Assert.True(Path.IsPathRooted(resource.ManifestPath));
        Assert.Equal(absolutePath, resource.ManifestPath);
    }

    [Fact]
    public void AddManifestSetsParent()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        var absolutePath = Path.Combine(AppContext.BaseDirectory, "manifests", "crds.yaml");
        cluster.AddManifest("crds", absolutePath);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var manifestResource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        var clusterResource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        Assert.Same(clusterResource, manifestResource.Parent);
    }

    [Fact]
    public void AddManifestFromContentCreatesResource()
    {
        const string content = "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: aspire-demo";
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifestFromContent("demo-ns", content);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal("demo-ns", resource.Name);
        Assert.Equal(K8sManifestResource.InlineManifestPath, resource.ManifestPath);
        Assert.Equal(content, resource.InlineContent);
        Assert.False(resource.IsKustomize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddManifestRegistersPostApplyCheckWithoutRecurringHealth(bool inline)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");
        if (inline)
        {
            cluster.AddManifestFromContent("crds", "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: test");
        }
        else
        {
            cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));
        }

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<K8sManifestResource>());
        Assert.Empty(resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Single(app.Services.GetServices<IKindPostApplyCheck>());
    }

    [Fact]
    public async Task ManifestPostApplyCheckWaitsForAllAppliedCrds()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, """
            customresourcedefinition.apiextensions.k8s.io/widgets.example.com
            namespace/example
            customresourcedefinition.apiextensions.k8s.io/gadgets.example.com
            customresourcedefinition.apiextensions.k8s.io/widgets.example.com
            """, ""));
        var kubernetes = FakeCrdClient.Established("widgets.example.com", "gadgets.example.com");
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml")).Resource;
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(runner);

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);

        Assert.Equal(2, runner.Commands.Count);
        Assert.Equal(2, kubernetes.ReadCount);
        Assert.Equal(["gadgets.example.com", "widgets.example.com"], kubernetes.ReadNames.Order());
    }

    [Fact]
    public async Task ManifestPostApplyCheckOnlyRereadsRemainingCrdsUntilEstablished()
    {
        var gadgetsReads = 0;
        var kubernetes = new FakeCrdClient((name, _) => Task.FromResult(name switch
        {
            "widgets.example.com" => FakeCrdClient.Definition(name, established: true),
            "gadgets.example.com" => FakeCrdClient.Definition(
                name, established: Interlocked.Increment(ref gadgetsReads) > 1),
            _ => throw new InvalidOperationException($"Unexpected CRD: {name}"),
        }));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml")).Resource;
        var factoryCalls = 0;
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return kubernetes.Client;
        });
        using var app = builder.Build();
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames =
        [
            "customresourcedefinition.apiextensions.k8s.io/widgets.example.com",
            "customresourcedefinition.apiextensions.k8s.io/gadgets.example.com",
        ];

        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);

        Assert.Equal(3, kubernetes.ReadCount);
        Assert.Equal(1, kubernetes.ReadNames.Count(name => name == "widgets.example.com"));
        Assert.Equal(2, kubernetes.ReadNames.Count(name => name == "gadgets.example.com"));
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, kubernetes.DisposeCount);
    }

    [Fact]
    public async Task ManifestPostApplyCheckRetriesNotFoundUntilCrdIsEstablished()
    {
        var reads = 0;
        var kubernetes = new FakeCrdClient((name, _) =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                return Task.FromException<V1CustomResourceDefinition>(new HttpOperationException
                {
                    Response = new HttpResponseMessageWrapper(
                        new HttpResponseMessage(HttpStatusCode.NotFound), "CRD not found"),
                });
            }

            return Task.FromResult(FakeCrdClient.Definition(name, established: true));
        });
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml")).Resource;
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames =
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"];

        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);

        Assert.Equal(["widgets.example.com", "widgets.example.com"], kubernetes.ReadNames);
    }

    [Fact]
    public async Task ManifestPostApplyCheckRejectsNamesAcceptedFalseWithoutWaitingForTimeout()
    {
        var kubernetes = new FakeCrdClient((name, _) => Task.FromResult(
            FakeCrdClient.Definition(name, established: false, namesAccepted: false)));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml")).Resource;
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames =
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"];
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token));

        Assert.Contains("rejected", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("widgets.example.com", error.Message);
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
    }

    [Fact]
    public async Task ManifestPostApplyCheckReportsRejectedNameBeforeOtherReadCompletes()
    {
        TaskCompletionSource<V1CustomResourceDefinition> slowRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource enteredSlowRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var kubernetes = new FakeCrdClient((name, _) =>
        {
            if (name == "gadgets.example.com")
            {
                enteredSlowRead.TrySetResult();
                return slowRead.Task;
            }

            return Task.FromResult(FakeCrdClient.Definition(name, established: false, namesAccepted: false));
        });
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml")).Resource;
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames =
        [
            "customresourcedefinition.apiextensions.k8s.io/widgets.example.com",
            "customresourcedefinition.apiextensions.k8s.io/gadgets.example.com",
        ];
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        var check = app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token);
        try
        {
            await enteredSlowRead.Task.WaitAsync(cts.Token);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => check.WaitAsync(cts.Token));
            Assert.Contains("widgets.example.com", error.Message);
            Assert.Equal(0, kubernetes.DisposeCount);
        }
        finally
        {
            slowRead.TrySetResult(FakeCrdClient.Definition("gadgets.example.com", established: true));
        }

        Assert.True(SpinWait.SpinUntil(() => kubernetes.DisposeCount == 1, TimeSpan.FromSeconds(5)));
        Assert.Equal(2, kubernetes.ReadCount);
    }

    [Fact]
    public async Task ManifestPostApplyCheck_DistinguishesPendingFromSuccessfulApplyWithoutCrds()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "namespace/example", ""));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifestFromContent("namespace", "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: example").Resource;
        builder.Services.AddSingleton<IProcessRunner>(runner);
        using var app = builder.Build();
        var outcomes = KindDeploymentOutcomes.GetOrCreate(resource);
        Assert.Null(outcomes.CrdNames);

        using var loggerFactory = LoggerFactory.Create(_ => { });
        await new KubectlManager(runner).ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);

        Assert.Empty(outcomes.CrdNames!);
        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);
        Assert.Equal(2, runner.Commands.Count);
    }

    [Fact]
    public async Task ManifestCrdCheckRejectsMissingApplyOutcome()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifestFromContent("manifest", "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: example").Resource;
        using var app = builder.Build();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken));

        Assert.Contains("has not completed applying", error.Message);
    }

    [Fact]
    public async Task ManifestCrdCheckDoesNotCreateMissingOutcomeAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifestFromContent("manifest", "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: example").Resource;
        var annotation = KindDeploymentOutcomes.GetOrCreate(resource);
        resource.Annotations.Remove(annotation);
        using var app = builder.Build();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken));

        Assert.Contains("has not completed applying", error.Message);
        Assert.False(resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out _));
    }

    [Fact]
    public async Task ManifestPostApplyCheck_RetainsLastSuccessfulCrdsAfterFailedReapply()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(1, "customresourcedefinition.apiextensions.k8s.io/gadgets.example.com", "forbidden"));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml")).Resource;
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(runner);
        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken));
        Assert.Equal(
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"],
            KindDeploymentOutcomes.GetOrCreate(resource).CrdNames);
        Assert.Equal(4, runner.Commands.Count);
    }

    [Fact]
    public async Task ManifestCrdFailureFailsToStartWithoutRunning()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient((name, _) => Task.FromResult(
            FakeCrdClient.Definition(name, established: false)));
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
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithCrdWait(options => options.Timeout = TimeSpan.FromSeconds(1));
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var startTask = app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync("crds", KnownResourceStates.FailedToStart, cts.Token);
        await startTask;

        Assert.True(notifications.TryGetCurrentState("crds", out var current));
        Assert.Equal(KnownResourceStates.FailedToStart, current.Snapshot.State?.Text);
        Assert.Equal(2, runner.Commands.Count);
        Assert.True(kubernetes.ReadCount > 0);
    }

    [Fact]
    public async Task WaitForManifestHoldsDependentUntilCrdsAreEstablished()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        TaskCompletionSource<bool> waitingForCrd = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> establishCrd = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var kubernetes = new FakeCrdClient(async (name, cancellationToken) =>
        {
            waitingForCrd.TrySetResult(true);
            await establishCrd.Task.WaitAsync(cancellationToken);
            return FakeCrdClient.Definition(name, established: true);
        });
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
        var crds = cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));
        var dependent = builder.AddResource(new ManifestDependentResource("consumer"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Manifest Dependent",
                State = KnownResourceStates.NotStarted,
                Properties = [],
            })
            .WaitFor(crds);
        dependent.OnInitializeResource(async (resource, e, ct) =>
        {
            await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, e.Services), ct);
            await e.Notifications.PublishUpdateAsync(resource, state => state with { State = KnownResourceStates.Running });
        });
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var startTask = app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        try
        {
            await waitingForCrd.Task.WaitAsync(cts.Token);
            await notifications.WaitForResourceAsync("consumer", KnownResourceStates.Waiting, cts.Token);
            Assert.Equal(2, runner.Commands.Count);
        }
        finally
        {
            establishCrd.TrySetResult(true);
        }

        await notifications.WaitForResourceHealthyAsync("crds", cts.Token);
        await notifications.WaitForResourceHealthyAsync("consumer", cts.Token);
        await startTask;
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
    }

    [Fact]
    public void AddManifestThrowsOnNullBuilder()
    {
        IResourceBuilder<KindClusterResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddManifest("crds", @"C:\manifests\crds.yaml"));
    }

    [Fact]
    public void AddManifestThrowsOnNullName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");
        Assert.Throws<ArgumentNullException>(() => cluster.AddManifest(null!, @"C:\manifests\crds.yaml"));
    }

    [Fact]
    public void AddManifestThrowsOnNullManifestPath()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentNullException>(() => cluster.AddManifest("crds", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddManifestThrowsOnWhitespaceManifestPath(string manifestPath)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentException>(() => cluster.AddManifest("crds", manifestPath));
    }

    [Fact]
    public void AddManifestFromContentThrowsOnNullBuilder()
    {
        IResourceBuilder<KindClusterResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddManifestFromContent("crds", "apiVersion: v1"));
    }

    [Fact]
    public void AddManifestFromContentThrowsOnNullName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");
        Assert.Throws<ArgumentNullException>(() => cluster.AddManifestFromContent(null!, "apiVersion: v1"));
    }

    [Fact]
    public void AddManifestFromContentThrowsOnNullContent()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");
        Assert.Throws<ArgumentNullException>(() => cluster.AddManifestFromContent("crds", null!));
    }

    [Fact]
    public void AddManifestRejectsRelativePath()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        var exception = Assert.Throws<ArgumentException>(() => cluster.AddManifest("crds", Path.Combine("manifests", "crds.yaml")));

        Assert.Contains("absolute path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddManifestUsesAbsolutePathAsIs()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var absolutePath = Path.Combine(AppContext.BaseDirectory, "manifests", "crds.yaml");

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", absolutePath);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(absolutePath, resource.ManifestPath);
    }

    [Fact]
    public void AddManifestDetectsKustomizationYaml()
    {
        var directory = CreateTestDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "kustomization.yaml"), "resources: []");
            var resource = AddManifestAndGetResource(directory);
            var args = KubectlManager.CreateApplyArguments(resource);

            Assert.True(resource.IsKustomize);
            Assert.Contains("-k", args);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AddManifestDetectsKustomizationYml()
    {
        var directory = CreateTestDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "kustomization.yml"), "resources: []");
            var resource = AddManifestAndGetResource(directory);
            var args = KubectlManager.CreateApplyArguments(resource);

            Assert.True(resource.IsKustomize);
            Assert.Contains("-k", args);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AddManifestOnDirectoryWithoutKustomization_IsNotKustomize()
    {
        var directory = CreateTestDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "manifest.yaml"), "apiVersion: v1");
            var resource = AddManifestAndGetResource(directory);
            var args = KubectlManager.CreateApplyArguments(resource);

            Assert.False(resource.IsKustomize);
            Assert.Contains("-f", args);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WithRecursiveSetsRecursive()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("all", Path.Combine(AppContext.BaseDirectory, "manifests"))
            .WithRecursive();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.True(resource.Recursive);
    }

    [Fact]
    public void WithServerSideApplySetsServerSide()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithServerSideApply();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.True(resource.ServerSide);
        Assert.False(resource.ForceConflicts);
    }

    [Fact]
    public void WithServerSideApplyForceConflictsSetsBoth()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithServerSideApply(forceConflicts: true);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.True(resource.ServerSide);
        Assert.True(resource.ForceConflicts);
    }

    [Fact]
    public void WithFieldManagerSetsFieldManager()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithFieldManager("my-tool");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal("my-tool", resource.FieldManager);
    }

    [Fact]
    public void WithApplyTimeoutSetsApplyTimeout()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithApplyTimeout(TimeSpan.FromSeconds(30));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(30), resource.ApplyTimeout);
    }

    [Fact]
    public void WithClusterReadyTimeoutSetsClusterReadyTimeout()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithClusterReadyTimeout(TimeSpan.FromSeconds(90));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(90), resource.ClusterReadyTimeout);
    }

    [Fact]
    public void WithClusterReadyTimeoutWiresValueIntoKubectlManagerCreation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithClusterReadyTimeout(TimeSpan.FromSeconds(90));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());

        var manager = KindManifestResourceBuilderExtensions.CreateKubectlManager(new FakeProcessRunner(), resource);

        Assert.Equal(TimeSpan.FromSeconds(90), manager.ClusterInfoMaxWaitForTesting);
    }

    [Fact]
    public void WithClusterReadyTimeoutRejectsZero()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
                .WithClusterReadyTimeout(TimeSpan.Zero));
    }

    [Fact]
    public void WithClusterReadyTimeoutRejectsNegative()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
                .WithClusterReadyTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WithClusterReadyTimeoutRoundsSubSecondUpToOneSecond()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithClusterReadyTimeout(TimeSpan.FromMilliseconds(500));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(1), resource.ClusterReadyTimeout);
    }

    [Fact]
    public void WithClusterReadyTimeoutRejectsMoreThanOneHour()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
                .WithClusterReadyTimeout(TimeSpan.MaxValue));
    }

    [Fact]
    public void WithApplyTimeoutRejectsZero()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
                .WithApplyTimeout(TimeSpan.Zero));
    }

    [Fact]
    public void WithApplyTimeoutRejectsNegative()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
                .WithApplyTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WithApplyTimeoutRoundsSubSecondUpToOneSecond()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithApplyTimeout(TimeSpan.FromMilliseconds(500));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(1), resource.ApplyTimeout);
    }

    [Fact]
    public void WithApplyTimeoutRejectsMoreThanOneHour()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
                .WithApplyTimeout(TimeSpan.MaxValue));
    }

    [Fact]
    public void CrdWaitOptionsUseExpectedDefaults()
    {
        var options = new CrdWaitOptions();

        Assert.Equal(TimeSpan.FromMinutes(5), options.Timeout);
        Assert.Equal(CrdWaitBehavior.Fail, options.FailureBehavior);
    }

    [Fact]
    public void CrdWaitOptionsTimeoutRoundsFractionalSecondsAtAssignment()
    {
        var options = new CrdWaitOptions
        {
            Timeout = TimeSpan.FromMilliseconds(500),
        };

        Assert.Equal(TimeSpan.FromSeconds(1), options.Timeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3_600_001)]
    public void CrdWaitOptionsTimeoutRejectsInvalidAssignment(long milliseconds)
    {
        var options = new CrdWaitOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            options.Timeout = TimeSpan.FromMilliseconds(milliseconds));
        Assert.Equal(TimeSpan.FromMinutes(5), options.Timeout);
    }

    [Fact]
    public void WithCrdWaitUsesDefaultsForNewPolicy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));

        manifest.WithCrdWait(_ => { });

        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.Equal(TimeSpan.FromMinutes(5), policy.Options.Timeout);
        Assert.Equal(CrdWaitBehavior.Fail, policy.Options.FailureBehavior);
    }

    [Fact]
    public void WithCrdWaitInvokesCallbackOnce()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));
        var callbackCount = 0;

        manifest.WithCrdWait(_ => callbackCount++);

        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public void WithCrdWaitConfiguresManifestPolicyWithInferredResourceType()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var cluster = builder.AddKindCluster("test-cluster");
        var manifest = cluster.AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));
        Assert.Same(manifest, manifest.WithCrdWait(options =>
        {
            options.Timeout = TimeSpan.FromSeconds(45);
            options.FailureBehavior = CrdWaitBehavior.BestEffort;
        }));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.True(resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.Equal(TimeSpan.FromSeconds(45), policy.Options.Timeout);
        Assert.Equal(CrdWaitBehavior.BestEffort, policy.Options.FailureBehavior);
    }

    [Fact]
    public void WithCrdWaitAnnotationStoresExactCallbackOptionsInstance()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));
        CrdWaitOptions? configuredOptions = null;

        manifest.WithCrdWait(options =>
        {
            configuredOptions = options;
            options.Timeout = TimeSpan.FromMilliseconds(500);
        });

        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.NotNull(configuredOptions);
        Assert.Same(configuredOptions, policy.Options);
        Assert.Equal(TimeSpan.FromSeconds(1), configuredOptions.Timeout);
    }

    [Fact]
    public void CapturedCrdWaitOptionsRejectInvalidMutationAfterConfiguration()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));
        CrdWaitOptions? configuredOptions = null;
        manifest.WithCrdWait(options =>
        {
            configuredOptions = options;
            options.Timeout = TimeSpan.FromSeconds(45);
        });
        Assert.NotNull(configuredOptions);
        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));

        Assert.Throws<ArgumentOutOfRangeException>(() => configuredOptions.Timeout = TimeSpan.Zero);

        Assert.Same(configuredOptions, policy.Options);
        Assert.Equal(TimeSpan.FromSeconds(45), policy.Options.Timeout);
    }

    [Fact]
    public void FailedWithCrdWaitConfigurationKeepsOriginalOptionsReference()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));
        CrdWaitOptions? originalOptions = null;
        manifest.WithCrdWait(options =>
        {
            originalOptions = options;
            options.Timeout = TimeSpan.FromSeconds(45);
        });
        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        CrdWaitOptions? failedOptions = null;

        Assert.Throws<InvalidOperationException>(() => manifest.WithCrdWait(options =>
        {
            failedOptions = options;
            options.Timeout = TimeSpan.FromSeconds(30);
            throw new InvalidOperationException("Configuration failed.");
        }));

        Assert.NotNull(originalOptions);
        Assert.NotNull(failedOptions);
        Assert.NotSame(originalOptions, failedOptions);
        Assert.Same(originalOptions, policy.Options);
        Assert.Equal(TimeSpan.FromSeconds(45), policy.Options.Timeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3_600_001)]
    public void WithCrdWaitRejectsInvalidTimeoutWithoutAttachingPolicy(long milliseconds)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manifest.WithCrdWait(options => options.Timeout = TimeSpan.FromMilliseconds(milliseconds)));
        Assert.False(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out _));
    }

    [Fact]
    public void WithCrdWaitRoundsFractionalSecondsUp()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));

        manifest.WithCrdWait(options => options.Timeout = TimeSpan.FromMilliseconds(500));

        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.Equal(TimeSpan.FromSeconds(1), policy.Options.Timeout);
        Assert.Equal(CrdWaitBehavior.Fail, policy.Options.FailureBehavior);
    }

    [Fact]
    public void RepeatedWithCrdWaitCallsPreserveUnspecifiedValues()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));

        manifest.WithCrdWait(options => options.Timeout = TimeSpan.FromSeconds(45));
        manifest.WithCrdWait(options => options.FailureBehavior = CrdWaitBehavior.BestEffort);

        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.Equal(TimeSpan.FromSeconds(45), policy.Options.Timeout);
        Assert.Equal(CrdWaitBehavior.BestEffort, policy.Options.FailureBehavior);
    }

    [Fact]
    public void WithCrdWaitCallbackFailureDoesNotChangeExistingPolicy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithCrdWait(options =>
            {
                options.Timeout = TimeSpan.FromSeconds(45);
                options.FailureBehavior = CrdWaitBehavior.BestEffort;
            });
        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var originalPolicy));
        var originalOptions = originalPolicy.Options;

        Assert.Throws<InvalidOperationException>(() => manifest.WithCrdWait(options =>
        {
            options.Timeout = TimeSpan.FromSeconds(30);
            options.FailureBehavior = CrdWaitBehavior.Fail;
            Assert.Equal(TimeSpan.FromSeconds(45), originalPolicy.Options.Timeout);
            Assert.Equal(CrdWaitBehavior.BestEffort, originalPolicy.Options.FailureBehavior);
            throw new InvalidOperationException("Configuration failed.");
        }));

        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var currentPolicy));
        Assert.Same(originalPolicy, currentPolicy);
        Assert.Same(originalOptions, currentPolicy.Options);
        Assert.Equal(TimeSpan.FromSeconds(45), currentPolicy.Options.Timeout);
        Assert.Equal(CrdWaitBehavior.BestEffort, currentPolicy.Options.FailureBehavior);
    }

    [Fact]
    public void WithCrdWaitValidationFailureDoesNotChangeExistingPolicy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithCrdWait(options =>
            {
                options.Timeout = TimeSpan.FromSeconds(45);
                options.FailureBehavior = CrdWaitBehavior.BestEffort;
            });
        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var originalPolicy));
        var originalOptions = originalPolicy.Options;

        Assert.Throws<ArgumentOutOfRangeException>(() => manifest.WithCrdWait(options =>
        {
            options.Timeout = TimeSpan.Zero;
            options.FailureBehavior = CrdWaitBehavior.Fail;
        }));

        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var currentPolicy));
        Assert.Same(originalPolicy, currentPolicy);
        Assert.Same(originalOptions, currentPolicy.Options);
        Assert.Equal(TimeSpan.FromSeconds(45), currentPolicy.Options.Timeout);
        Assert.Equal(CrdWaitBehavior.BestEffort, currentPolicy.Options.FailureBehavior);
    }

    [Fact]
    public void DefaultRecursiveIsFalse()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.False(resource.Recursive);
    }

    [Fact]
    public void DefaultServerSideIsFalse()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.False(resource.ServerSide);
        Assert.False(resource.ForceConflicts);
    }

    [Fact]
    public void DefaultFieldManagerIsNull()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.Null(resource.FieldManager);
    }

    [Fact]
    public void DefaultApplyTimeoutIsFiveMinutes()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.Equal(TimeSpan.FromMinutes(5), resource.ApplyTimeout);
    }

    [Fact]
    public void DefaultClusterReadyTimeoutIsSixtySeconds()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.Equal(TimeSpan.FromSeconds(60), resource.ClusterReadyTimeout);
    }

    [Fact]
    public void LegacyManifestCrdWaitPropertiesSharePolicy()
    {
        var resource = new K8sManifestResource("crds", "./crds.yaml", new KindClusterResource("cluster"));

#pragma warning disable CS0618 // Exercise the shipped manifest API.
        Assert.Equal(TimeSpan.FromMinutes(5), resource.CrdWaitTimeout);
        Assert.Equal(CrdWaitBehavior.Fail, resource.CrdWaitBehavior);
        Assert.False(resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out _));
        resource.CrdWaitTimeout = TimeSpan.FromMilliseconds(500);
        resource.CrdWaitBehavior = (CrdWaitBehavior)42;
        Assert.Throws<ArgumentOutOfRangeException>(() => resource.CrdWaitTimeout = TimeSpan.Zero);
#pragma warning restore CS0618

        Assert.True(resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.Equal(TimeSpan.FromSeconds(1), policy.Options.Timeout);
        Assert.Equal((CrdWaitBehavior)42, policy.Options.FailureBehavior);
    }

    [Fact]
    public void LegacyManifestCrdWaitMethodsSharePolicy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var manifest = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"));

#pragma warning disable CS0618 // Exercise the shipped manifest overloads.
        Assert.Same(manifest, KindManifestResourceBuilderExtensions.WithCrdWaitTimeout(manifest, TimeSpan.FromMilliseconds(500)));
        Assert.Same(manifest, KindManifestResourceBuilderExtensions.WithCrdWaitBehavior(manifest, CrdWaitBehavior.BestEffort));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KindManifestResourceBuilderExtensions.WithCrdWaitTimeout(manifest, TimeSpan.Zero));
#pragma warning restore CS0618

        Assert.True(manifest.Resource.TryGetLastAnnotation<KindCrdWaitPolicyAnnotation>(out var policy));
        Assert.Equal(TimeSpan.FromSeconds(1), policy.Options.Timeout);
        Assert.Equal(CrdWaitBehavior.BestEffort, policy.Options.FailureBehavior);
    }

    [Fact]
    public void DefaultNamespaceIsNull()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.Null(resource.Namespace);
    }

    [Fact]
    public void ManifestResourceIsIResourceWithParent()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.IsAssignableFrom<IResourceWithParent<KindClusterResource>>(resource);
        Assert.Same(cluster, ((IResourceWithParent<KindClusterResource>)resource).Parent);
    }

    // ── KubectlManager argument-shape tests (no CLI invocation) ──────────────────

    [Fact]
    public void CreateApplyArguments_MinimalManifest_ContainsApplyAndKubeconfig()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Equal("apply", args[0]);
        Assert.Contains("-f", args);
        Assert.Contains("./crds.yaml", args);
        Assert.Contains(args, a => a.StartsWith("--kubeconfig=", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateApplyArguments_WithNamespace_IncludesNamespaceFlag()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster)
        {
            Namespace = "kube-system",
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Contains("--namespace", args);
        Assert.Contains("kube-system", args);
    }

    [Fact]
    public void CreateApplyArguments_WithRecursive_IncludesRecursiveFlag()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("all", "./manifests", cluster)
        {
            Recursive = true,
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Contains("--recursive", args);
    }

    [Fact]
    public void CreateApplyArguments_KustomizeMode_Uses_MinusK()
    {
        var cluster = new KindClusterResource("test-cluster");
        var directory = CreateTestDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "kustomization.yaml"), "resources: []");
            var resource = new K8sManifestResource("kustom", directory, cluster);

            var args = KubectlManager.CreateApplyArguments(resource);

            Assert.Contains("-k", args);
            Assert.Contains(directory, args);
            Assert.DoesNotContain("-f", args);
            Assert.True(resource.IsKustomize);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Kustomization")]
    [InlineData("Kustomization.yml")]
    [InlineData("KUSTOMIZATION.YAML")]
    public void CreateApplyArguments_KustomizeMode_DetectsCaseInsensitiveVariants(string fileName)
    {
        var cluster = new KindClusterResource("test-cluster");
        var directory = CreateTestDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, fileName), "resources: []");
            var resource = new K8sManifestResource("kustom", directory, cluster);

            var args = KubectlManager.CreateApplyArguments(resource);

            Assert.Contains("-k", args);
            Assert.True(resource.IsKustomize);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateApplyArguments_DetectsKustomizeAtApplyTime()
    {
        var directory = CreateTestDirectory();

        try
        {
            var resource = AddManifestAndGetResource(directory);
            Assert.False(resource.IsKustomize);

            File.WriteAllText(Path.Combine(directory, "kustomization.yaml"), "resources: []");

            var args = KubectlManager.CreateApplyArguments(resource);

            Assert.Contains("-k", args);
            Assert.True(resource.IsKustomize);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateApplyArguments_InlineContent_Uses_MinusStdinDash()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("inline", K8sManifestResource.InlineManifestPath, cluster)
        {
            InlineContent = "apiVersion: v1",
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Contains("-f", args);
        Assert.Contains("-", args);
        Assert.DoesNotContain(K8sManifestResource.InlineManifestPath, args);
    }

    [Fact]
    public void CreateApplyArguments_InlineContent_SkipsKustomizeDetection()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("inline", K8sManifestResource.InlineManifestPath, cluster)
        {
            InlineContent = "apiVersion: v1",
            IsKustomize = true,
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Contains("-f", args);
        Assert.Contains("-", args);
        Assert.DoesNotContain("-k", args);
    }

    [Fact]
    public void WithRecursive_OnKustomize_Warns_And_Ignores()
    {
        var cluster = new KindClusterResource("test-cluster");
        var directory = CreateTestDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "kustomization.yaml"), "resources: []");
            var resource = new K8sManifestResource("kustom", directory, cluster)
            {
                Recursive = true,
            };

            var args = KubectlManager.CreateApplyArguments(resource);

            Assert.DoesNotContain("--recursive", args);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateApplyArguments_WithServerSide_IncludesServerSideFlag()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster)
        {
            ServerSide = true,
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Contains("--server-side", args);
        Assert.DoesNotContain("--force-conflicts", args);
    }

    [Fact]
    public void CreateApplyArguments_ServerSideWithForceConflicts_IncludesBoth()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster)
        {
            ServerSide = true,
            ForceConflicts = true,
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Contains("--server-side", args);
        Assert.Contains("--force-conflicts", args);
    }

    [Fact]
    public void CreateApplyArguments_ForceConflictsWithoutServerSide_OmitsForceConflicts()
    {
        // --force-conflicts only means anything with --server-side; without server-side we
        // should not emit it, otherwise kubectl rejects the command.
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster)
        {
            ServerSide = false,
            ForceConflicts = true,
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.DoesNotContain("--force-conflicts", args);
    }

    [Fact]
    public void CreateApplyArguments_WithFieldManager_IncludesFieldManagerFlag()
    {
        var cluster = new KindClusterResource("test-cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster)
        {
            FieldManager = "my-tool",
        };

        var args = KubectlManager.CreateApplyArguments(resource);

        Assert.Contains("--field-manager", args);
        Assert.Contains("my-tool", args);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateKubectlArguments_RejectWhitespaceKubeconfigPath(string kubeconfigPath)
    {
        Assert.Throws<ArgumentException>(() => KubectlManager.CreateClusterInfoArguments(kubeconfigPath));
        Assert.Throws<ArgumentException>(() => KubectlManager.CreateGetCrdsArguments(kubeconfigPath));
    }

    [Fact]
    public async Task ManifestPostApplyCheck_BestEffortAllowsUnverifiedReadiness()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient((name, _) => Task.FromResult(
            FakeCrdClient.Definition(name, established: false)));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithCrdWait(options =>
            {
                options.FailureBehavior = CrdWaitBehavior.BestEffort;
                options.Timeout = TimeSpan.FromSeconds(1);
            }).Resource;
        builder.Services.AddSingleton<IProcessRunner>(processRunner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);

        Assert.Equal(2, processRunner.Commands.Count);
        Assert.True(kubernetes.ReadCount > 0);
    }

    [Theory]
    [InlineData(CrdWaitBehavior.Fail)]
    [InlineData(CrdWaitBehavior.BestEffort)]
    public async Task ManifestPostApplyCheck_HandlesTransportFailureAccordingToPolicy(CrdWaitBehavior behavior)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifestFromContent("crds", "kind: CustomResourceDefinition")
            .WithCrdWait(options => options.FailureBehavior = behavior).Resource;
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames =
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"];
        var kubernetes = new FakeCrdClient((_, _) =>
            Task.FromException<V1CustomResourceDefinition>(new HttpRequestException("connection refused")));
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();

        if (behavior == CrdWaitBehavior.BestEffort)
        {
            await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);
        }
        else
        {
            var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
                app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken));
            Assert.Contains("connection refused", error.Message);
        }

        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
    }

    [Fact]
    public async Task ApplyAsync_DoesNotWaitForAppliedCrds()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);
        var resource = new K8sManifestResource("crds", "./crds.yaml", new KindClusterResource("test-cluster"));

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None);

        Assert.Equal(2, processRunner.Commands.Count);
        Assert.Contains("--output=name", processRunner.Commands[1].Arguments);
    }

    [Fact]
    public async Task ManifestPostApplyCheck_StrictCrdWaitFailureThrows()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient((name, _) => Task.FromResult(
            FakeCrdClient.Definition(name, established: false)));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithCrdWait(options => options.Timeout = TimeSpan.FromSeconds(1)).Resource;
        builder.Services.AddSingleton<IProcessRunner>(processRunner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken));

        Assert.Contains("Established", error.Message);
        Assert.Equal(2, processRunner.Commands.Count);
        Assert.True(kubernetes.ReadCount > 0);
    }

    [Fact]
    public async Task ManifestPostApplyCheck_UsesParentKubeconfigWhenCreatingClient()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = FakeCrdClient.Established("widgets.example.com");
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml")).Resource;
        builder.Services.AddSingleton<IProcessRunner>(processRunner);
        string? kubeconfigPath = null;
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => path =>
        {
            kubeconfigPath = path;
            return kubernetes.Client;
        });
        using var app = builder.Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);

        Assert.Equal(resource.Parent.KubeconfigPath, kubeconfigPath);
        Assert.Equal(2, processRunner.Commands.Count);
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
    }

    [Theory]
    [InlineData(CrdWaitBehavior.Fail)]
    [InlineData(CrdWaitBehavior.BestEffort)]
    public async Task ManifestPostApplyCheck_StopsHungWaitAtConfiguredTimeout(CrdWaitBehavior behavior)
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient(async (name, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return FakeCrdClient.Definition(name, established: false);
        });
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifest("crds", Path.Combine(AppContext.BaseDirectory, "crds.yaml"))
            .WithCrdWait(options =>
            {
                options.Timeout = TimeSpan.FromSeconds(1);
                options.FailureBehavior = behavior;
            }).Resource;
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        await new KubectlManager(runner).ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        if (behavior == CrdWaitBehavior.BestEffort)
        {
            await app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token);
        }
        else
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token));
        }
        Assert.Equal(2, runner.Commands.Count);
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
    }

    [Fact]
    public async Task ManifestPostApplyCheck_RejectsEstablishedResponseAfterDeadline()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifestFromContent("crds", "kind: CustomResourceDefinition")
            .WithCrdWait(options => options.Timeout = TimeSpan.FromSeconds(1)).Resource;
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames =
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"];
        var kubernetes = new FakeCrdClient(async (name, _) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1250), TestContext.Current.CancellationToken);
            return FakeCrdClient.Definition(name, established: true);
        });
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplyAsync_RetriesClusterInfoBeforeApply()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(1, "", "not ready"));
        processRunner.Results.Enqueue(new(1, "", "still not ready"));
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "namespace/default unchanged", ""));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner, static (_, _) => Task.CompletedTask);
        var resource = new K8sManifestResource("manifest", "./manifest.yaml", new KindClusterResource("test-cluster"));

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None);

        Assert.Equal(4, processRunner.Commands.Count);
        Assert.All(processRunner.Commands.Take(3), command => Assert.Contains("cluster-info", command.Arguments));
        Assert.Contains("apply -f ./manifest.yaml", processRunner.Commands[3].Arguments);
    }

    [Fact]
    public async Task ApplyAsync_ClusterInfoSlowFailuresRespectWallClockBudget()
    {
        var processRunner = new FakeProcessRunner
        {
            NextResult = new(1, "", "not ready"),
            Delay = TimeSpan.FromMilliseconds(40),
        };
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(
            processRunner,
            static (_, _) => Task.CompletedTask,
            clusterInfoMaxWait: TimeSpan.FromMilliseconds(100),
            apiProbeTimeout: TimeSpan.FromSeconds(1));
        var resource = new K8sManifestResource("manifest", "./manifest.yaml", new KindClusterResource("test-cluster"));
        var started = DateTimeOffset.UtcNow;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None));

        var elapsed = DateTimeOffset.UtcNow - started;
        Assert.Contains("Timed out waiting for cluster", ex.Message);
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"Elapsed {elapsed} exceeded tolerance.");
        Assert.All(processRunner.Commands, command => Assert.Contains("cluster-info", command.Arguments));
    }

    [Fact]
    public async Task ApplyAsync_CancelsApplyAfterConfiguredTimeout()
    {
        var processRunner = new FakeProcessRunner
        {
            DelayAsync = static (_, cancellationToken) =>
            {
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
                return tcs.Task;
            }
        };
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "namespace/default unchanged", ""));
        processRunner.Delays.Enqueue(TimeSpan.Zero);
        processRunner.Delays.Enqueue(TimeSpan.FromSeconds(5));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);
        var resource = new K8sManifestResource("manifest", "./manifest.yaml", new KindClusterResource("test-cluster"))
        {
            ApplyTimeout = TimeSpan.FromMilliseconds(10),
        };

        await Assert.ThrowsAsync<TimeoutException>(
            () => manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAsync_WithRecursiveFilePath_ThrowsBeforeKubectlApply()
    {
        var directory = CreateTestDirectory();

        try
        {
            var manifestPath = Path.Combine(directory, "manifest.yaml");
            await File.WriteAllTextAsync(manifestPath, "apiVersion: v1");

            var processRunner = new FakeProcessRunner();
            processRunner.Results.Enqueue(new(0, "cluster is running", ""));
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var manager = new KubectlManager(processRunner);
            var resource = new K8sManifestResource("manifest", manifestPath, new KindClusterResource("test-cluster"))
            {
                Recursive = true,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None));

            Assert.Contains("existing directory", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(processRunner.Commands);
            Assert.Contains("cluster-info", processRunner.Commands[0].Arguments);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_IncludesStdoutWhenApplyFailsWithoutStderr()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(1, "resource mapping not found", ""));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);
        var resource = new K8sManifestResource("manifest", "./manifest.yaml", new KindClusterResource("test-cluster"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None));

        Assert.Contains("resource mapping not found", ex.Message);
        Assert.False(resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out _));
    }

    [Fact]
    public async Task ApplyAsync_ThrowsHelpfulErrorWhenKubectlIsMissing()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(new ThrowingProcessRunner(new Win32Exception("kubectl not found")));
        var resource = new K8sManifestResource("manifest", "./manifest.yaml", new KindClusterResource("test-cluster"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None));

        Assert.Contains("kubectl CLI not found", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_InlineContent_PassesStandardInput()
    {
        const string content = "apiVersion: v1\nkind: Namespace";
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "", ""));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);
        var resource = new K8sManifestResource("inline", K8sManifestResource.InlineManifestPath, new KindClusterResource("test-cluster"))
        {
            InlineContent = content,
        };

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None);

        var command = processRunner.Commands.Last();
        Assert.Contains("apply -f -", command.Arguments);
        Assert.Equal(content, command.StandardInput);
    }

    [Fact]
    public void CreateApplyArguments_ThrowsOnNullResource()
    {
        Assert.Throws<ArgumentNullException>(() => KubectlManager.CreateApplyArguments(null!));
    }

    private static K8sManifestResource AddManifestAndGetResource(string path)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("kustom", path);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        return Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "kind-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class ManifestDependentResource(string name) : Resource(name), IResourceWithWaitSupport;

    private sealed class ThrowingProcessRunner(Exception exception) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            ILogger logger,
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string>? environmentVariables = null,
            string? standardInput = null,
            CancellationToken cancellationToken = default) => Task.FromException<ProcessResult>(exception);
    }
}