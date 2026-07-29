// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    public async Task WithKubernetesVersionSetsVersion()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster")
            .WithKubernetesVersion("v1.32.2");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains($"image: {"kindest/node"}:v1.32.2", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithNodeImageSetsImageOnAllNodes()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster")
            .WithWorkerNodes(2)
            .WithNodeImage("myacr.azurecr.io/kindest/node:v1.32.2");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Equal(3, yaml.Split("image: myacr.azurecr.io/kindest/node:v1.32.2").Length - 1);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithNodeMountAddsMountOnAllNodes()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster")
            .WithWorkerNodes(1)
            .WithNodeMount(@"C:\host-data", "/container-data", readOnly: true);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Equal(2, yaml.Split("hostPath: C:\\host-data").Length - 1);
            Assert.Equal(2, yaml.Split("containerPath: /container-data").Length - 1);
            Assert.Equal(2, yaml.Split("readOnly: true").Length - 1);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithWorkerNodesSetsCount()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster")
            .WithWorkerNodes(3);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            var workerCount = yaml.Split("- role: worker").Length - 1;
            Assert.Equal(3, workerCount);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task DefaultsAreCorrect()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddKindCluster("test-cluster");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());
        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("- role: control-plane", yaml);
            Assert.DoesNotContain("- role: worker", yaml);
            Assert.DoesNotContain("image:", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
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
    public void WithNodeImageRejectsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentNullException>(() => cluster.WithNodeImage(null!));
    }

    [Fact]
    public void WithNodeMountRejectsNullHostPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentNullException>(() => cluster.WithNodeMount(null!, "/container-data"));
    }

    [Fact]
    public void WithNodeMountRejectsNullContainerPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentNullException>(() => cluster.WithNodeMount(@"C:\host-data", null!));
    }

    [Fact]
    public async Task GeneratedConfigContainsImageFromKindContainerImageTags()
    {
        var resource = new KindClusterResource("test-cluster");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            foreach (var node in config.Nodes)
            {
                node.Image = $"{"kindest/node"}:v1.32.0";
            }
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);

        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            var expectedImage = $"{"kindest/node"}:v1.32.0";
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
    public async Task WithReference_NonContainer_SetsHostKubeconfigEnvironmentValue()
    {
        var builder = DistributedApplication.CreateBuilder();

        var kind = builder.AddKindCluster("test-cluster");
        var service = builder.AddResource(new TestResource("svc"))
            .WithReference(kind);

        using var app = builder.Build();

        var environment = await service.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Run);
        Assert.Equal(kind.Resource.KubeconfigPath, environment["KUBECONFIG"]);
        Assert.Equal("test-cluster", environment["K8S_CLUSTER_NAME"]);
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

    [Fact]
    public async Task KindClusterLifecycleHook_CleansUpOnApplicationStopping()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddKindCluster("test-cluster");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var loggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var processRunner = new FakeProcessRunner();
        var hostLifetime = new TestHostApplicationLifetime();
        var hook = new KindClusterLifecycleHook(
            model,
            loggerService,
            processRunner,
            new TestKindContainerRuntimeResolver(),
            hostLifetime);

        await hook.SubscribeAsync(new NoOpEventing(), null!);
        hostLifetime.StopApplication();
        await hook.DisposeAsync();

        Assert.Single(
            processRunner.Commands,
            command => command.FileName == "kind" && command.Arguments.Contains("delete cluster --name=test-cluster", StringComparison.Ordinal));
    }

    [Fact]
    public async Task KindClusterLifecycleHook_DoesNotDeletePersistentClustersOnApplicationStopping()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddKindCluster("persistent-cluster")
            .WithClusterLifetime(ClusterLifetime.Persistent);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var loggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var processRunner = new FakeProcessRunner();
        var hostLifetime = new TestHostApplicationLifetime();
        var hook = new KindClusterLifecycleHook(
            model,
            loggerService,
            processRunner,
            new TestKindContainerRuntimeResolver(),
            hostLifetime);

        await hook.SubscribeAsync(new NoOpEventing(), null!);
        hostLifetime.StopApplication();
        await hook.DisposeAsync();

        Assert.DoesNotContain(processRunner.Commands, command => command.FileName == "kind" && command.Arguments.Contains("delete cluster", StringComparison.Ordinal));
    }

    // ── KindConfigGenerator tests ────────────────────────────────────────

    [Fact]
    public async Task GenerateConfig_ZeroWorkers_OnlyControlPlane()
    {
        var resource = new KindClusterResource("cfg-zero");

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("- role: control-plane", yaml);
            Assert.DoesNotContain("- role: worker", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task GenerateConfig_ThreeWorkers_HasControlPlaneAndThreeWorkers()
    {
        var resource = new KindClusterResource("cfg-three");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            for (int i = 0; i < 3; i++)
            {
                config.Nodes.Add(new KindNodeModel { Role = "worker" });
            }
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("- role: control-plane", yaml);

            var workerCount = yaml.Split("- role: worker").Length - 1;
            Assert.Equal(3, workerCount);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task GenerateConfig_NoK8sVersion_OmitsImageLine()
    {
        var resource = new KindClusterResource("cfg-noversion");

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.DoesNotContain("image:", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task GenerateConfig_WithK8sVersion_IncludesImageLine()
    {
        var resource = new KindClusterResource("cfg-version");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            foreach (var node in config.Nodes)
            {
                node.Image = $"{"kindest/node"}:v1.31.0";
            }
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains($"image: {"kindest/node"}:v1.31.0", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    // ── WithKindConfig / KindConfigAnnotation tests ────────────────────

    [Fact]
    public async Task WithKindConfig_AddsExtraPortMapping_AppearsInYaml()
    {
        var resource = new KindClusterResource("cfg-port");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            config.Nodes[0].ExtraPortMappings =
            [
                new KindPortMappingModel { ContainerPort = 80, HostPort = 8080, Protocol = "TCP" }
            ];
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("containerPort: 80", yaml);
            Assert.Contains("hostPort: 8080", yaml);
            Assert.Contains("protocol: TCP", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithKindConfig_SetsFeatureGate_AppearsInYaml()
    {
        var resource = new KindClusterResource("cfg-fg");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            config.FeatureGates = new Dictionary<string, bool> { ["SomeAlphaFeature"] = true };
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("featureGates:", yaml);
            Assert.Contains("SomeAlphaFeature: true", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithKindConfig_DisableDefaultCNI_AppearsInYaml()
    {
        var resource = new KindClusterResource("cfg-cni");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            config.Networking = new KindNetworkingModel { DisableDefaultCNI = true };
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("networking:", yaml);
            Assert.Contains("disableDefaultCNI: true", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithKindConfig_AddsExtraMount_AppearsInYaml()
    {
        var resource = new KindClusterResource("cfg-mount");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            config.Nodes[0].ExtraMounts =
            [
                new KindMountModel { HostPath = "/tmp/data", ContainerPath = "/data" }
            ];
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("hostPath: /tmp/data", yaml);
            Assert.Contains("containerPath: /data", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithKindConfig_AddsNodeLabels_AppearsInYaml()
    {
        var resource = new KindClusterResource("cfg-labels");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            config.Nodes[0].Labels = new Dictionary<string, string> { ["my-label"] = "my-value" };
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("labels:", yaml);
            Assert.Contains("my-label: my-value", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task WithKindConfig_MultipleCalls_AllAppliedInOrder()
    {
        var resource = new KindClusterResource("cfg-multi");
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            config.FeatureGates = new Dictionary<string, bool> { ["FeatureA"] = true };
        }));
        resource.Annotations.Add(new KindConfigAnnotation(config =>
        {
            config.Networking = new KindNetworkingModel { PodSubnet = "10.244.0.0/16" };
        }));

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("FeatureA: true", yaml);
            Assert.Contains("podSubnet: 10.244.0.0/16", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    // ── WithKubernetesVersion + WithWorkerNodes interaction test ────────

    [Fact]
    public async Task WithKubernetesVersionAndWorkerNodes_BothOrders_AllNodesHaveImage()
    {
        var expectedImage = $"{"kindest/node"}:v1.31.0";

        // Order 1: workers first, then version
        var builder1 = DistributedApplication.CreateBuilder();
        builder1.AddKindCluster("order1")
            .WithWorkerNodes(2)
            .WithKubernetesVersion("v1.31.0");

        using var app1 = builder1.Build();
        var resource1 = Assert.Single(app1.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<KindClusterResource>());

        var path1 = await KindConfigGenerator.GenerateConfigAsync(resource1, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(path1);
            Assert.Equal(3, yaml.Split(expectedImage).Length - 1); // control-plane + 2 workers
        }
        finally { File.Delete(path1); }

        // Order 2: version first, then workers
        var builder2 = DistributedApplication.CreateBuilder();
        builder2.AddKindCluster("order2")
            .WithKubernetesVersion("v1.31.0")
            .WithWorkerNodes(2);

        using var app2 = builder2.Build();
        var resource2 = Assert.Single(app2.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<KindClusterResource>());

        var path2 = await KindConfigGenerator.GenerateConfigAsync(resource2, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(path2);
            Assert.Equal(3, yaml.Split(expectedImage).Length - 1); // control-plane + 2 workers
        }
        finally { File.Delete(path2); }
    }

    // ── DefaultProcessRunner tests ──────────────────────────────────────

    [Fact]
    public async Task DefaultProcessRunner_CapturesStdout()
    {
        var runner = new DefaultProcessRunner();

        var result = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? await runner.RunAsync(NullLogger.Instance, "cmd", ["/c", "echo", "hello"])
            : await runner.RunAsync(NullLogger.Instance, "sh", ["-c", "echo hello"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Output);
    }

    [Fact]
    public async Task DefaultProcessRunner_InvalidCommand_NonZeroExitCode()
    {
        var runner = new DefaultProcessRunner();

        var result = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? await runner.RunAsync(NullLogger.Instance, "cmd", ["/c", "exit", "1"])
            : await runner.RunAsync(NullLogger.Instance, "sh", ["-c", "exit 1"]);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task DefaultProcessRunner_RedactsKubeconfigPathInDebugLog()
    {
        var runner = new DefaultProcessRunner();
        var logger = new CapturingLogger();
        const string kubeconfigPath = "C:\\Users\\tamirdresher\\.kube\\kind-config.yaml";

        _ = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? await runner.RunAsync(logger, "cmd", ["/c", "echo", "hello", $"--kubeconfig={kubeconfigPath}"])
            : await runner.RunAsync(logger, "sh", ["-c", "echo hello", $"--kubeconfig={kubeconfigPath}"]);

        var executingMessage = Assert.Single(logger.Messages, message => message.StartsWith("Executing:", StringComparison.Ordinal));
        Assert.DoesNotContain(kubeconfigPath, executingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--kubeconfig=[REDACTED]", executingMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultProcessRunner_RedactsSpaceSeparatedKubeconfigPathInDebugLog()
    {
        var runner = new DefaultProcessRunner();
        var logger = new CapturingLogger();
        const string kubeconfigPath = "C:\\Users\\tamirdresher\\.kube\\kind-config.yaml";

        _ = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? await runner.RunAsync(logger, "cmd", ["/c", "echo", "hello", "--kubeconfig", kubeconfigPath])
            : await runner.RunAsync(logger, "sh", ["-c", "echo hello", "--kubeconfig", kubeconfigPath]);

        var executingMessage = Assert.Single(logger.Messages, message => message.StartsWith("Executing:", StringComparison.Ordinal));
        Assert.DoesNotContain(kubeconfigPath, executingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--kubeconfig [REDACTED]", executingMessage, StringComparison.Ordinal);
    }

    // ── Edge-case tests ──────────────────────────────────────────────────

    [Fact]
    public async Task WithWorkerNodes_Zero_IsValid()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddKindCluster("edge-zero")
            .WithWorkerNodes(0);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<KindClusterResource>());

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("- role: control-plane", yaml);
            Assert.DoesNotContain("- role: worker", yaml);
        }

        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public void KindClusterResource_Constructor_SetsPathsCorrectly()
    {
        var resource = new KindClusterResource("path-check");

        var expectedDir = Path.Combine(Path.GetTempPath(), "aspire-kind", "path-check");
        Assert.Equal(Path.Combine(expectedDir, "kubeconfig.yaml"), resource.KubeconfigPath);
        Assert.Equal(Path.Combine(expectedDir, "container-kubeconfig.yaml"), resource.ContainerKubeconfigPath);
    }

    private sealed class TestResource(string name) : Resource(name), IResourceWithEnvironment;

    private sealed class TestKindContainerRuntimeResolver : IKindContainerRuntimeResolver
    {
        public Task<KindContainerRuntime> ResolveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new KindContainerRuntime("docker"));
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _applicationStarted = new();
        private readonly CancellationTokenSource _applicationStopping = new();
        private readonly CancellationTokenSource _applicationStopped = new();

        public CancellationToken ApplicationStarted => _applicationStarted.Token;

        public CancellationToken ApplicationStopping => _applicationStopping.Token;

        public CancellationToken ApplicationStopped => _applicationStopped.Token;

        public void StopApplication()
        {
            if (!_applicationStopping.IsCancellationRequested)
            {
                _applicationStopping.Cancel();
            }
        }
    }

    private sealed class NoOpEventing : IDistributedApplicationEventing
    {
        public DistributedApplicationEventSubscription Subscribe<T>(Func<T, CancellationToken, Task> callback)
            where T : IDistributedApplicationEvent => null!;

        public DistributedApplicationEventSubscription Subscribe<T>(IResource resource, Func<T, CancellationToken, Task> callback)
            where T : IDistributedApplicationResourceEvent => null!;

        public void Unsubscribe(DistributedApplicationEventSubscription subscription)
        {
        }

        public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
            where T : IDistributedApplicationEvent => Task.CompletedTask;

        public Task PublishAsync<T>(T @event, EventDispatchBehavior dispatchBehavior, CancellationToken cancellationToken = default)
            where T : IDistributedApplicationEvent => Task.CompletedTask;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
