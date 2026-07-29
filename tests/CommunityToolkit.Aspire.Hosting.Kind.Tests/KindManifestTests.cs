// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindManifestTests
{
    [Fact]
    public void AddManifestCreatesResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./manifests/crds.yaml");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal("crds", resource.Name);
        Assert.True(Path.IsPathRooted(resource.ManifestPath));
        Assert.EndsWith(Path.Combine("manifests", "crds.yaml"), resource.ManifestPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddManifestSetsParent()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./manifests/crds.yaml");

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
        var builder = DistributedApplication.CreateBuilder();

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

    [Fact]
    public void AddManifestThrowsOnNullBuilder()
    {
        IResourceBuilder<KindClusterResource> builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddManifest("crds", "./crds.yaml"));
    }

    [Fact]
    public void AddManifestThrowsOnNullName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");
        Assert.Throws<ArgumentNullException>(() => cluster.AddManifest(null!, "./crds.yaml"));
    }

    [Fact]
    public void AddManifestThrowsOnNullManifestPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");
        Assert.Throws<ArgumentNullException>(() => cluster.AddManifest("crds", null!));
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
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");
        Assert.Throws<ArgumentNullException>(() => cluster.AddManifestFromContent(null!, "apiVersion: v1"));
    }

    [Fact]
    public void AddManifestFromContentThrowsOnNullContent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");
        Assert.Throws<ArgumentNullException>(() => cluster.AddManifestFromContent("crds", null!));
    }

    [Fact]
    public void AddManifestUsesAppHostRelativePath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var relativePath = Path.Combine("manifests", "crds.yaml");
        var expected = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, relativePath));

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", relativePath);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(expected, resource.ManifestPath);
    }

    [Fact]
    public void AddManifestUsesAbsolutePathAsIs()
    {
        var builder = DistributedApplication.CreateBuilder();
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
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("all", "./manifests")
            .WithRecursive();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.True(resource.Recursive);
    }

    [Fact]
    public void WithServerSideApplySetsServerSide()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
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
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
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
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithFieldManager("my-tool");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal("my-tool", resource.FieldManager);
    }

    [Fact]
    public void WithApplyTimeoutSetsApplyTimeout()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithApplyTimeout(TimeSpan.FromSeconds(30));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(30), resource.ApplyTimeout);
    }

    [Fact]
    public void WithClusterReadyTimeoutSetsClusterReadyTimeout()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithClusterReadyTimeout(TimeSpan.FromSeconds(90));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(90), resource.ClusterReadyTimeout);
    }

    [Fact]
    public void WithClusterReadyTimeoutRejectsZero()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithClusterReadyTimeout(TimeSpan.Zero));
    }

    [Fact]
    public void WithClusterReadyTimeoutRejectsNegative()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithClusterReadyTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WithClusterReadyTimeoutRoundsSubSecondUpToOneSecond()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithClusterReadyTimeout(TimeSpan.FromMilliseconds(500));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(1), resource.ClusterReadyTimeout);
    }

    [Fact]
    public void WithClusterReadyTimeoutRejectsMoreThanOneHour()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithClusterReadyTimeout(TimeSpan.MaxValue));
    }

    [Fact]
    public void WithApplyTimeoutRejectsZero()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithApplyTimeout(TimeSpan.Zero));
    }

    [Fact]
    public void WithApplyTimeoutRejectsNegative()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithApplyTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WithApplyTimeoutRoundsSubSecondUpToOneSecond()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithApplyTimeout(TimeSpan.FromMilliseconds(500));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(1), resource.ApplyTimeout);
    }

    [Fact]
    public void WithApplyTimeoutRejectsMoreThanOneHour()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithApplyTimeout(TimeSpan.MaxValue));
    }

    [Fact]
    public void WithCrdWaitTimeoutSetsCrdWaitTimeout()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithCrdWaitTimeout(TimeSpan.FromSeconds(45));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(45), resource.CrdWaitTimeout);
    }

    [Fact]
    public void WithCrdWaitTimeoutRejectsZero()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithCrdWaitTimeout(TimeSpan.Zero));
    }

    [Fact]
    public void WithCrdWaitTimeoutRejectsNegative()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithCrdWaitTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WithCrdWaitTimeoutRoundsSubSecondUpToOneSecond()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithCrdWaitTimeout(TimeSpan.FromMilliseconds(500));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(TimeSpan.FromSeconds(1), resource.CrdWaitTimeout);
    }

    [Fact]
    public void WithCrdWaitTimeoutRejectsMoreThanOneHour()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cluster = builder.AddKindCluster("test-cluster");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cluster.AddManifest("crds", "./crds.yaml")
                .WithCrdWaitTimeout(TimeSpan.MaxValue));
    }

    [Fact]
    public void WithCrdWaitBehaviorSetsCrdWaitBehavior()
    {
        var builder = DistributedApplication.CreateBuilder();

        var cluster = builder.AddKindCluster("test-cluster");
        cluster.AddManifest("crds", "./crds.yaml")
            .WithCrdWaitBehavior(CrdWaitBehavior.BestEffort);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<K8sManifestResource>());
        Assert.Equal(CrdWaitBehavior.BestEffort, resource.CrdWaitBehavior);
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
    public void DefaultCrdWaitSettingsFailAfterFiveMinutes()
    {
        var cluster = new KindClusterResource("cluster");
        var resource = new K8sManifestResource("crds", "./crds.yaml", cluster);

        Assert.Equal(TimeSpan.FromMinutes(5), resource.CrdWaitTimeout);
        Assert.Equal(CrdWaitBehavior.Fail, resource.CrdWaitBehavior);
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

    [Fact]
    public void CreateWaitArguments_ProducesExpectedShape()
    {
        var args = KubectlManager.CreateWaitArguments(
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com", "customresourcedefinition.apiextensions.k8s.io/gadgets.example.com"],
            "C:\\kube\\config.yaml",
            TimeSpan.FromMinutes(5));

        Assert.Equal("wait", args[0]);
        Assert.Equal("--for=condition=Established", args[1]);
        Assert.Contains("customresourcedefinition.apiextensions.k8s.io/widgets.example.com", args);
        Assert.Contains("customresourcedefinition.apiextensions.k8s.io/gadgets.example.com", args);
        Assert.Contains("--timeout=300s", args);
        Assert.Contains("--kubeconfig=C:\\kube\\config.yaml", args);
    }

    [Fact]
    public void CreateWaitArguments_RoundsSubSecondTimeoutUpToOneSecond()
    {
        var args = KubectlManager.CreateWaitArguments(
            ["customresourcedefinition.apiextensions.k8s.io/widgets.example.com"],
            "C:\\kube\\config.yaml",
            TimeSpan.FromMilliseconds(500));

        Assert.Contains("--timeout=1s", args);
    }

    [Fact]
    public async Task ApplyAsync_WaitsForAppliedCrdsBestEffort()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com created", ""));
        processRunner.Results.Enqueue(new(1, "", "timed out waiting for the condition"));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);
        var resource = new K8sManifestResource("crds", "./crds.yaml", new KindClusterResource("test-cluster"))
        {
            CrdWaitBehavior = CrdWaitBehavior.BestEffort,
        };

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None);

        Assert.Equal(3, processRunner.Commands.Count);
        Assert.Contains("cluster-info", processRunner.Commands[0].Arguments);
        Assert.Contains("apply -f ./crds.yaml", processRunner.Commands[1].Arguments);
        Assert.Contains("wait --for=condition=Established customresourcedefinition.apiextensions.k8s.io/widgets.example.com", processRunner.Commands[2].Arguments);
    }

    [Fact]
    public async Task ApplyAsync_FailsWhenCrdWaitFailsByDefault()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com created", ""));
        processRunner.Results.Enqueue(new(1, "", "timed out waiting for the condition"));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);
        var resource = new K8sManifestResource("crds", "./crds.yaml", new KindClusterResource("test-cluster"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None));

        Assert.Contains("Established", ex.Message);
        Assert.Equal(3, processRunner.Commands.Count);
    }

    [Fact]
    public async Task ApplyAsync_UsesConfiguredCrdWaitTimeout()
    {
        var processRunner = new FakeProcessRunner();
        processRunner.Results.Enqueue(new(0, "cluster is running", ""));
        processRunner.Results.Enqueue(new(0, "customresourcedefinition.apiextensions.k8s.io/widgets.example.com created", ""));
        processRunner.Results.Enqueue(new(0, "", ""));
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var manager = new KubectlManager(processRunner);
        var resource = new K8sManifestResource("crds", "./crds.yaml", new KindClusterResource("test-cluster"))
        {
            CrdWaitTimeout = TimeSpan.FromSeconds(42),
        };

        await manager.ApplyAsync(resource, loggerFactory.CreateLogger("test"), CancellationToken.None);

        Assert.Contains("--timeout=42s", processRunner.Commands[2].Arguments);
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
            clusterInfoProbeTimeout: TimeSpan.FromSeconds(1));
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
        var processRunner = new FakeProcessRunner();
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
        var builder = DistributedApplication.CreateBuilder();
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
}