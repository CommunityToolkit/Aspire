// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using k8s;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindPostApplyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializerRunsRegisteredChecksInOrderBeforeRunning(bool helm)
    {
        var runner = new FakeProcessRunner();
        if (helm)
        {
            runner.Results.Enqueue(new(0, "", ""));
            runner.Results.Enqueue(new(0, "release installed", ""));
        }
        else
        {
            runner.Results.Enqueue(new(0, "cluster is running", ""));
        }
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = FakeCrdClient.Established("widgets.example.com");
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        List<string> observed = [];
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck(async (deployed, token) =>
        {
            Assert.Same(resource, deployed);
            Assert.True(token.CanBeCanceled);
            Assert.Equal(
                ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"],
                KindDeploymentOutcomes.GetOrCreate(deployed).CrdNames);
            observed.Add("first");
            entered.SetResult();
            await release.Task.WaitAsync(token);
        }));
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck((deployed, _) =>
        {
            Assert.Same(resource, deployed);
            observed.Add("second");
            return Task.CompletedTask;
        }));
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        var start = app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        try
        {
            await entered.Task.WaitAsync(cts.Token);
            Assert.True(notifications.TryGetCurrentState(resource.Name, out var current));
            Assert.Equal(KnownResourceStates.Starting, current.Snapshot.State?.Text);
            Assert.Equal(["first"], observed);
        }
        finally
        {
            release.TrySetResult();
        }

        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.Running, cts.Token);
        await start;
        Assert.Equal(["first", "second"], observed);
        Assert.Equal(helm ? 1 : 0, runner.Commands.Count(command => command.FileName == "helm"));
        Assert.True(runner.Commands.Count >= (helm ? 3 : 2));
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializerWithoutChecksStillRunsAfterSuccessfulApply(bool helm)
    {
        var runner = new FakeProcessRunner();
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.Running, cts.Token);
        Assert.NotNull(KindDeploymentOutcomes.GetOrCreate(resource).CrdNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveredCrdsAreEstablishedBeforeRunningOnlyOnce(bool helm)
    {
        var runner = new FakeProcessRunner();
        if (helm)
        {
            runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/existing.example.com", ""));
            runner.Results.Enqueue(new(0, "release installed", ""));
            runner.Results.Enqueue(new(0, """
                customresourcedefinition.apiextensions.k8s.io/existing.example.com
                customresourcedefinition.apiextensions.k8s.io/widgets.example.com
                customresourcedefinition.apiextensions.k8s.io/gadgets.example.com
                """, ""));
        }
        else
        {
            runner.Results.Enqueue(new(0, "cluster is running", ""));
            runner.Results.Enqueue(new(0, """
                customresourcedefinition.apiextensions.k8s.io/widgets.example.com
                namespace/example
                customresourcedefinition.apiextensions.k8s.io/gadgets.example.com
                """, ""));
        }
        TaskCompletionSource enteredWidget = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource enteredGadget = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseGadget = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var kubernetes = new FakeCrdClient(async (name, token) =>
        {
            if (name == "gadgets.example.com")
            {
                enteredGadget.TrySetResult();
                await releaseGadget.Task.WaitAsync(token);
            }
            else if (name == "widgets.example.com")
            {
                enteredWidget.TrySetResult();
            }
            return FakeCrdClient.Definition(name, established: true);
        });
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        var start = app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        try
        {
            await enteredGadget.Task.WaitAsync(cts.Token);
            await enteredWidget.Task.WaitAsync(cts.Token);
            Assert.True(notifications.TryGetCurrentState(resource.Name, out var pending));
            Assert.Equal(KnownResourceStates.Starting, pending.Snapshot.State?.Text);
            Assert.Equal(helm ? 3 : 2, runner.Commands.Count);
            Assert.Equal(["gadgets.example.com", "widgets.example.com"], kubernetes.ReadNames.Order());
        }
        finally
        {
            releaseGadget.TrySetResult();
        }

        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.Running, cts.Token);
        await start;
        Assert.Equal(2, kubernetes.ReadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoDiscoveredCrdsSkipsClient(bool helm)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        var runner = new FakeProcessRunner();
        var kubernetes = FakeCrdClient.Established();
        var created = 0;
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ =>
        {
            Interlocked.Increment(ref created);
            return kubernetes.Client;
        });
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        await app.Services.GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync(resource.Name, KnownResourceStates.Running, cts.Token);
        Assert.Empty(KindDeploymentOutcomes.GetOrCreate(resource).CrdNames!);
        Assert.Equal(0, kubernetes.ReadCount);
        Assert.Equal(0, created);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StrictCrdWaitFailurePreventsRunning(bool helm)
    {
        var runner = new FakeProcessRunner();
        if (helm)
        {
            runner.Results.Enqueue(new(0, "", ""));
            runner.Results.Enqueue(new(0, "release installed", ""));
        }
        else
        {
            runner.Results.Enqueue(new(0, "cluster is running", ""));
        }
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient((_, _) => Task.FromException<k8s.Models.V1CustomResourceDefinition>(
            new InvalidOperationException("CRD read failed")));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.FailedToStart, cts.Token);
        var annotation = KindDeploymentOutcomes.GetOrCreate(resource);
        Assert.Equal(
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"],
            annotation.CrdNames);
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
        Assert.Equal(helm ? 3 : 2, runner.Commands.Count);
    }

    [Fact]
    public async Task BestEffortWarnsThatCrdReadinessIsUnverifiedBeforeRunning()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        var kubernetes = new FakeCrdClient((name, _) => Task.FromResult(
            FakeCrdClient.Definition(name, established: false)));
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddResource(new KindClusterResource("test-cluster"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Kind Cluster",
                State = KnownResourceStates.Running,
                Properties = [],
            });
        var resource = cluster.AddManifestFromContent("manifest", "kind: Namespace")
            .WithCrdWaitBehavior(CrdWaitBehavior.BestEffort)
            .WithCrdWaitTimeout(TimeSpan.FromSeconds(1)).Resource;
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        await app.Services.GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync(resource.Name, KnownResourceStates.Running, cts.Token);

        var loggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var unverified = false;
        await foreach (var batch in loggerService.WatchAsync(resource.Name).WithCancellation(cts.Token))
        {
            unverified |= batch.Any(log => log.Content.Contains("unverified", StringComparison.OrdinalIgnoreCase)
                && log.Content.Contains(resource.Name));
            if (unverified)
            {
                break;
            }
        }
        Assert.True(unverified);
        Assert.True(kubernetes.ReadCount > 0);
        Assert.Equal(2, runner.Commands.Count);
    }

    [Fact]
    public async Task CancelingBestEffortCrdWaitPropagatesCancellation()
    {
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com", ""));
        TaskCompletionSource enteredWait = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var kubernetes = new FakeCrdClient(async (name, token) =>
        {
            enteredWait.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return FakeCrdClient.Definition(name, established: true);
        });
        using var builder = TestDistributedApplicationBuilder.Create();
        var cluster = builder.AddResource(new KindClusterResource("test-cluster"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Kind Cluster",
                State = KnownResourceStates.Running,
                Properties = [],
            });
        var resource = cluster.AddManifestFromContent("manifest", "kind: Namespace")
            .WithCrdWaitBehavior(CrdWaitBehavior.BestEffort).Resource;
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { });
        await new KubectlManager(runner).ApplyAsync(
            resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        var check = app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token);

        await enteredWait.Task.WaitAsync(cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);

        Assert.Equal(
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"],
            KindDeploymentOutcomes.GetOrCreate(resource).CrdNames);
        Assert.Equal(["widgets.example.com"], kubernetes.ReadNames);
        Assert.Equal(2, runner.Commands.Count);
    }

    [Fact]
    public async Task CanceledNoCrdCheckDoesNotSucceed()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifestFromContent("manifest", "kind: Namespace").Resource;
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames = [];
        using var app = builder.Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledBestEffortReadDoesNotSwallowFailure(bool transportFailure)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = builder.AddKindCluster("test-cluster")
            .AddManifestFromContent("manifest", "kind: CustomResourceDefinition")
            .WithCrdWaitBehavior(CrdWaitBehavior.BestEffort).Resource;
        KindDeploymentOutcomes.GetOrCreate(resource).CrdNames =
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"];
        using var cts = new CancellationTokenSource();
        var kubernetes = new FakeCrdClient((_, _) =>
        {
            cts.Cancel();
            return Task.FromException<V1CustomResourceDefinition>(transportFailure
                ? new HttpRequestException("connection failed")
                : new InvalidOperationException("invalid CRD response"));
        });
        builder.Services.AddSingleton<Func<string, IKubernetes>>(_ => _ => kubernetes.Client);
        using var app = builder.Build();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            app.Services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token));
        Assert.Equal(1, kubernetes.ReadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HelmCrdSnapshotFailureFailsToStartWithoutCheckingReadiness(bool afterInstall)
    {
        var runner = new FakeProcessRunner();
        if (afterInstall)
        {
            runner.Results.Enqueue(new(0, "", ""));
            runner.Results.Enqueue(new(0, "release installed", ""));
        }
        runner.Results.Enqueue(new(1, "", "forbidden"));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm: true);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        await app.Services.GetRequiredService<ResourceNotificationService>()
            .WaitForResourceAsync(resource.Name, KnownResourceStates.FailedToStart, cts.Token);
        Assert.False(resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out _));
        Assert.Equal(afterInstall ? 1 : 0, runner.Commands.Count(command => command.FileName == "helm"));
        Assert.Equal(afterInstall ? 3 : 1, runner.Commands.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializerLeavesNotStartedUntilParentAndBeforeStartEvent(bool helm)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var parent = builder.AddResource(new KindClusterResource("test-cluster"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Kind Cluster",
                State = KnownResourceStates.NotStarted,
                Properties = [],
            });
        KindDeployedResource resource;
        if (helm)
        {
            resource = parent.AddHelmChart("chart", "chart/ref").Resource;
        }
        else
        {
            resource = parent.AddManifestFromContent("manifest", "kind: Namespace").Resource;
        }
        var beforeStartState = "";
        builder.Eventing.Subscribe<BeforeResourceStartedEvent>(resource, (evt, _) =>
        {
            var notifications = evt.Services.GetRequiredService<ResourceNotificationService>();
            Assert.True(notifications.TryGetCurrentState(resource.Name, out var current));
            beforeStartState = current.Snapshot.State?.Text ?? "";
            return Task.CompletedTask;
        });
        builder.Services.AddSingleton<IProcessRunner>(new FakeProcessRunner());
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        var snapshots = app.Services.GetRequiredService<ResourceNotificationService>();
        Assert.True(snapshots.TryGetCurrentState(resource.Name, out var waiting));
        Assert.Equal(KnownResourceStates.NotStarted, waiting.Snapshot.State?.Text);
        await snapshots.PublishUpdateAsync(parent.Resource, snapshot => snapshot with { State = KnownResourceStates.Running });
        await snapshots.WaitForResourceAsync(resource.Name, KnownResourceStates.Running, cts.Token);
        Assert.Equal(KnownResourceStates.NotStarted, beforeStartState);
    }

    [Fact]
    public async Task HelmWaitForRequiresPostApplyAndWorkloadHealth()
    {
        var runner = new FakeProcessRunner();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var builder = TestDistributedApplicationBuilder.Create();
        var chart = AddDeployment(builder, helm: true);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck(async (_, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
        }));
        var workloadReady = 0;
        builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            var registration = Assert.Single(options.Registrations, item => item.Name == "helm_chart");
            options.Registrations.Remove(registration);
            options.Registrations.Add(new HealthCheckRegistration(
                "helm_chart",
                _ => new StubHealthCheck(() => Volatile.Read(ref workloadReady) == 1),
                failureStatus: null,
                tags: null,
                timeout: null));
        });
        var dependent = builder.AddResource(new CheckDependentResource("consumer"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Consumer",
                State = KnownResourceStates.NotStarted,
                Properties = [],
            })
            .WaitFor(builder.CreateResourceBuilder(chart));
        dependent.OnInitializeResource(async (consumer, e, token) =>
        {
            await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(consumer, e.Services), token);
            await e.Notifications.PublishUpdateAsync(consumer, state => state with { State = KnownResourceStates.Running });
        });
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var start = app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        try
        {
            await entered.Task.WaitAsync(cts.Token);
            await notifications.WaitForResourceAsync("consumer", KnownResourceStates.Waiting, cts.Token);
            Assert.True(notifications.TryGetCurrentState(chart.Name, out var pending));
            Assert.Equal(KnownResourceStates.Starting, pending.Snapshot.State?.Text);
            Assert.Equal(0, Volatile.Read(ref workloadReady));
        }
        finally
        {
            release.TrySetResult();
        }

        await notifications.WaitForResourceAsync(chart.Name, KnownResourceStates.Running, cts.Token);
        Assert.True(notifications.TryGetCurrentState("consumer", out var blocked));
        Assert.NotEqual(KnownResourceStates.Running, blocked.Snapshot.State?.Text);
        Volatile.Write(ref workloadReady, 1);
        await notifications.WaitForResourceHealthyAsync("consumer", cts.Token);
        await start;
        Assert.True(notifications.TryGetCurrentState("consumer", out var released));
        Assert.Equal(KnownResourceStates.Running, released.Snapshot.State?.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedApplySkipsChecksAndFailsToStart(bool helm)
    {
        var runner = new FakeProcessRunner();
        if (!helm)
        {
            runner.Results.Enqueue(new(0, "cluster is running", ""));
        }
        else
        {
            runner.Results.Enqueue(new(0, "", ""));
        }
        runner.Results.Enqueue(new(1, "", "apply failed"));
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        var called = false;
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck((_, _) =>
        {
            called = true;
            return Task.CompletedTask;
        }));
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.FailedToStart, cts.Token);
        Assert.False(called);
        Assert.Null(KindDeploymentOutcomes.GetOrCreate(resource).CrdNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckFailurePreventsRunningAndFailsToStart(bool helm)
    {
        var runner = new FakeProcessRunner();
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        var failure = new InvalidOperationException("required check failed");
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck((_, _) => Task.FromException(failure)));
        var nextCheckCalled = false;
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck((_, _) =>
        {
            nextCheckCalled = true;
            return Task.CompletedTask;
        }));
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        await app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.FailedToStart, cts.Token);
        Assert.True(notifications.TryGetCurrentState(resource.Name, out var current));
        Assert.Equal(KnownResourceStates.FailedToStart, current.Snapshot.State?.Text);
        Assert.False(nextCheckCalled);
        Assert.NotNull(KindDeploymentOutcomes.GetOrCreate(resource).CrdNames);
    }

    [Fact]
    public async Task RequiredCheckKeepsWaitForDependentWaitingAndNeverReleasesItOnFailure()
    {
        var runner = new FakeProcessRunner();
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm: false);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource fail = new(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck(async (_, token) =>
        {
            entered.SetResult();
            await fail.Task.WaitAsync(token);
            throw new InvalidOperationException("required check failed");
        }));
        var dependentStarted = false;
        var dependent = builder.AddResource(new CheckDependentResource("consumer"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Consumer",
                State = KnownResourceStates.NotStarted,
                Properties = [],
            })
            .WaitFor(builder.CreateResourceBuilder(resource));
        dependent.OnInitializeResource(async (consumer, e, token) =>
        {
            await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(consumer, e.Services), token);
            dependentStarted = true;
            await e.Notifications.PublishUpdateAsync(consumer, state => state with { State = KnownResourceStates.Running });
        });
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        var start = app.StartAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        try
        {
            await entered.Task.WaitAsync(cts.Token);
            await notifications.WaitForResourceAsync("consumer", KnownResourceStates.Waiting, cts.Token);
            Assert.True(notifications.TryGetCurrentState(resource.Name, out var current));
            Assert.Equal(KnownResourceStates.Starting, current.Snapshot.State?.Text);
            Assert.False(dependentStarted);
        }
        finally
        {
            fail.TrySetResult();
        }

        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.FailedToStart, cts.Token);
        await start;
        Assert.False(dependentStarted);
        Assert.True(notifications.TryGetCurrentState("consumer", out var consumerState));
        Assert.NotEqual(KnownResourceStates.Running, consumerState.Snapshot.State?.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledCheckFailsToStartWithoutRunning(bool helm)
    {
        var runner = new FakeProcessRunner();
        using var builder = TestDistributedApplicationBuilder.Create();
        var resource = AddDeployment(builder, helm);
        builder.Services.AddSingleton<IProcessRunner>(runner);
        using var checkCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<OperationCanceledException> canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.Services.AddSingleton<IKindPostApplyCheck>(new DelegateCheck(async (_, token) =>
        {
            Assert.True(token.CanBeCanceled);
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, checkCts.Token);
            }
            catch (OperationCanceledException exception)
            {
                canceled.TrySetResult(exception);
                throw;
            }
        }));
        using var app = builder.Build();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        var start = app.StartAsync(cts.Token);
        await entered.Task.WaitAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(resource.Name, out var pending));
        Assert.Equal(KnownResourceStates.Starting, pending.Snapshot.State?.Text);

        checkCts.Cancel();
        using var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var exception = await canceled.Task.WaitAsync(waitCts.Token);
        Assert.True(exception.CancellationToken.IsCancellationRequested);
        try
        {
            await start;
        }
        catch (OperationCanceledException)
        {
            // Aspire may finish host startup before the asynchronous resource initializer completes.
        }
        await notifications.WaitForResourceAsync(resource.Name, KnownResourceStates.FailedToStart, waitCts.Token);
        Assert.True(notifications.TryGetCurrentState(resource.Name, out var current));
        Assert.Equal(KnownResourceStates.FailedToStart, current.Snapshot.State?.Text);
        Assert.NotNull(KindDeploymentOutcomes.GetOrCreate(resource).CrdNames);
        Assert.Equal(helm ? 1 : 0, runner.Commands.Count(command => command.FileName == "helm"));
    }

    [Fact]
    public async Task FailedCheckPropagatesTheOriginalException()
    {
        var resource = new K8sManifestResource("manifest", "<inline>", new KindClusterResource("cluster"));
        var failure = new InvalidOperationException("required check failed");
        using var services = new ServiceCollection()
            .AddSingleton<IKindPostApplyCheck>(new DelegateCheck((_, _) => Task.FromException(failure)))
            .AddSingleton<KindPostApplyChecks>()
            .BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken));
        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task CanceledCheckPropagatesCancellation()
    {
        var resource = new K8sManifestResource("manifest", "<inline>", new KindClusterResource("cluster"));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var services = new ServiceCollection()
            .AddSingleton<IKindPostApplyCheck>(new DelegateCheck((_, token) =>
            {
                cts.Cancel();
                return Task.FromCanceled(token);
            }))
            .AddSingleton<KindPostApplyChecks>()
            .BuildServiceProvider();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, cts.Token));
    }

    [Fact]
    public async Task DispatcherRunsChecksWithoutValidatingOrCreatingAnOutcome()
    {
        var resource = new K8sManifestResource("manifest", "<inline>", new KindClusterResource("cluster"));
        var calls = 0;
        using var noChecks = new ServiceCollection()
            .AddSingleton<KindPostApplyChecks>()
            .BuildServiceProvider();
        using var services = new ServiceCollection()
            .AddSingleton<IKindPostApplyCheck>(new DelegateCheck((deployed, _) =>
            {
                Assert.Same(resource, deployed);
                calls++;
                return Task.CompletedTask;
            }))
            .AddSingleton<KindPostApplyChecks>()
            .BuildServiceProvider();

        await noChecks.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);
        await services.GetRequiredService<KindPostApplyChecks>().RunAsync(resource, TestContext.Current.CancellationToken);

        Assert.Equal(1, calls);
        Assert.False(resource.TryGetLastAnnotation<KindDeploymentOutcomeAnnotation>(out _));
    }

    [Fact]
    public async Task SequentialDirectManagerFailureRetainsLastSuccessfulOutcome()
    {
        var resource = new K8sManifestResource("manifest", "<inline>", new KindClusterResource("cluster"));
        var runner = new FakeProcessRunner();
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/old.example.com", ""));
        runner.Results.Enqueue(new(0, "cluster is running", ""));
        runner.Results.Enqueue(new(1, "", "forbidden"));
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(runner);
        var annotation = KindDeploymentOutcomes.GetOrCreate(resource);
        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken);
        Assert.Equal(["customresourcedefinition.apiextensions.k8s.io/old.example.com"], annotation.CrdNames);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), TestContext.Current.CancellationToken));
        Assert.Equal(["customresourcedefinition.apiextensions.k8s.io/old.example.com"], annotation.CrdNames);
    }

    private static KindDeployedResource AddDeployment(IDistributedApplicationBuilder builder, bool helm)
    {
        var cluster = builder.AddResource(new KindClusterResource("test-cluster"))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Kind Cluster",
                State = KnownResourceStates.Running,
                Properties = [],
            });
        return helm
            ? cluster.AddHelmChart("chart", "chart/ref").Resource
            : cluster.AddManifestFromContent("manifest", "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: test").Resource;
    }

    private sealed class DelegateCheck(Func<KindDeployedResource, CancellationToken, Task> check) : IKindPostApplyCheck
    {
        public Task CheckAsync(KindDeployedResource resource, CancellationToken cancellationToken) =>
            check(resource, cancellationToken);
    }

    private sealed class CheckDependentResource(string name) : Resource(name), IResourceWithWaitSupport;

    private sealed class StubHealthCheck(Func<bool> ready) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ready() ? HealthCheckResult.Healthy("Workloads ready") : HealthCheckResult.Unhealthy("Workloads pending"));
    }
}
