// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;
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
    public async Task GeneratedConfigContainsImageFromKindContainerImageTags()
    {
        var resource = new KindClusterResource("test-cluster")
        {
            KubernetesVersion = KindContainerImageTags.DefaultKubernetesVersion,
        };

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);

        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
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

    // ── KindConfigGenerator tests ────────────────────────────────────────

    [Fact]
    public async Task GenerateConfig_ZeroWorkers_OnlyControlPlane()
    {
        var resource = new KindClusterResource("cfg-zero") { WorkerNodes = 0 };

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
        var resource = new KindClusterResource("cfg-three") { WorkerNodes = 3 };

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
        var resource = new KindClusterResource("cfg-noversion") { KubernetesVersion = null };

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
        var resource = new KindClusterResource("cfg-version") { KubernetesVersion = "v1.31.0" };

        var configPath = await KindConfigGenerator.GenerateConfigAsync(resource, CancellationToken.None);
        try
        {
            var yaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains($"image: {KindContainerImageTags.KindNodeImageRepository}:v1.31.0", yaml);
        }
        finally
        {
            File.Delete(configPath);
        }
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

    // ── Edge-case tests ──────────────────────────────────────────────────

    [Fact]
    public async Task WithWorkerNodes_Zero_IsValid()
    {
        var resource = new KindClusterResource("edge-zero") { WorkerNodes = 0 };

        Assert.Equal(0, resource.WorkerNodes);

        // Also verify config generation works with 0 workers
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
}
