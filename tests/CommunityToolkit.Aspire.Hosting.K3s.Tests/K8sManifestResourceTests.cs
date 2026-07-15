using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K8sManifestResourceTests
{
    // ── AddK8sManifest registration ───────────────────────────────────────────

    [Fact]
    public void AddK8sManifestAddsResourceWithCorrectName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddK8sManifest("widget-crd", "./k8s/widget-crd.yaml");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K8sManifestResource>());
        Assert.Equal("widget-crd", resource.Name);
    }

    [Fact]
    public void AddK8sManifestStoresAbsolutePath()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddK8sManifest("crds", "./k8s/crds/");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K8sManifestResource>());
        // Path is resolved to absolute at registration time.
        Assert.True(System.IO.Path.IsPathRooted(resource.Path));
        Assert.EndsWith(System.IO.Path.Combine("k8s", "crds"),
            resource.Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddK8sManifestParentIsCluster()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddK8sManifest("widget-crd", "./widget-crd.yaml");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K8sManifestResource>());
        Assert.Same(cluster.Resource, resource.Parent);
        Assert.IsAssignableFrom<IResourceWithParent<K3sClusterResource>>(resource);
    }

    [Fact]
    public void K8sManifestResourceIsContainerResource()
    {
        // K8sManifestResource extends ContainerResource — it runs bitnami/kubectl in Docker.
        // No host-side kubectl binary required. WaitForCompletion waits for exit code 0.
        var resource = new K8sManifestResource(
            "crd", "./crd.yaml", new K3sClusterResource("k8s"));

        Assert.IsAssignableFrom<ContainerResource>(resource);
    }

    [Fact]
    public void AddK8sManifestIsExcludedFromManifest()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddK8sManifest("widget-crd", "./widget-crd.yaml");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<K8sManifestResource>());
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, resource.Annotations);
    }

    [Fact]
    public void ClusterTracksRegisteredManifests()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddK8sManifest("widget-crd", "./widget-crd.yaml");
        cluster.AddK8sManifest("rbac", "./rbac.yaml");

        Assert.Contains("widget-crd", cluster.Resource.Manifests);
        Assert.Contains("rbac", cluster.Resource.Manifests);
        Assert.Equal(2, cluster.Resource.Manifests.Count);
    }

    [Fact]
    public void MultipleManifestsCanBeRegisteredOnSameCluster()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddK8sManifest("crd1", "./crd1.yaml");
        cluster.AddK8sManifest("crd2", "./crd2.yaml");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Equal(2, model.Resources.OfType<K8sManifestResource>().Count());
    }

    // ── Public API null-guard tests ───────────────────────────────────────────

    [Fact]
    public void AddK8sManifestShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;
        var action = () => builder.AddK8sManifest("crd", "./crd.yaml");
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddK8sManifestShouldThrowWhenNameIsNull()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var action = () => cluster.AddK8sManifest(null!, "./crd.yaml");
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddK8sManifestShouldThrowWhenPathIsNull()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var action = () => cluster.AddK8sManifest("crd", null!);
        Assert.Throws<ArgumentNullException>(action);
    }

    // ── File resolution ───────────────────────────────────────────────────────

    [Fact]
    public void ResolveFilesSingleFile()
    {
        // Create a temp file to test with
        var file = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(file, "apiVersion: v1\nkind: ConfigMap");

        try
        {
            var files = K3sManifestBuilderExtensions.ResolveFilesForTest(file);
            Assert.Single(files, f => f == file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveFilesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "b.yaml"), "");
        File.WriteAllText(Path.Combine(dir, "a.yaml"), "");
        File.WriteAllText(Path.Combine(dir, "c.yml"), "");

        try
        {
            var files = K3sManifestBuilderExtensions.ResolveFilesForTest(dir);

            // Lexicographic order, all YAML extensions
            Assert.Equal(3, files.Count);
            Assert.Equal("a.yaml", Path.GetFileName(files[0]));
            Assert.Equal("b.yaml", Path.GetFileName(files[1]));
            Assert.Equal("c.yml", Path.GetFileName(files[2]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Script generation ─────────────────────────────────────────────────────

    [Fact]
    public void BuildManifestScriptIncludesKubectlApply()
    {
        var script = K3sManifestBuilderExtensions.BuildManifestScript();
        Assert.Contains("kubectl apply -f /k8s-manifests", script);
        Assert.Contains("--server-side", script);
    }

    [Fact]
    public void BuildManifestScriptAutoDetectsKustomize()
    {
        // The script checks for kustomization.yaml at runtime — no path argument needed.
        var script = K3sManifestBuilderExtensions.BuildManifestScript();
        Assert.Contains("kustomization.yaml", script);
        Assert.Contains("kubectl apply -k /k8s-manifests", script);
    }

    [Fact]
    public void BuildManifestScriptWaitsForKubeconfigBeforeApplying()
    {
        var script = K3sManifestBuilderExtensions.BuildManifestScript();
        var kubeconfigWaitIndex = script.IndexOf("/root/.kube/kubeconfig.yaml", StringComparison.Ordinal);
        var applyIndex = script.IndexOf("kubectl apply", StringComparison.Ordinal);
        Assert.True(kubeconfigWaitIndex < applyIndex, "Kubeconfig wait must precede kubectl apply");
    }

    // ── Kustomize detection ───────────────────────────────────────────────────

    [Fact]
    public void ResolveFilesGlobPattern_ReturnsMatchingFilesOrdered()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"glob-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "c.yaml"), "");
        File.WriteAllText(Path.Combine(dir, "a.yaml"), "");
        File.WriteAllText(Path.Combine(dir, "b.yaml"), "");

        try
        {
            var globPath = Path.Combine(dir, "*.yaml");
            var files = K3sManifestBuilderExtensions.ResolveFilesForTest(globPath);

            Assert.Equal(3, files.Count);
            Assert.Equal("a.yaml", Path.GetFileName(files[0]));
            Assert.Equal("b.yaml", Path.GetFileName(files[1]));
            Assert.Equal("c.yaml", Path.GetFileName(files[2]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AddK8sManifestKustomizeDirectoryShowsKustomizeResourceType()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kustomize-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "kustomization.yaml"), "resources: []");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            var cluster = appBuilder.AddK3sCluster("k8s");
            cluster.AddK8sManifest("kustom", dir);

            using var app = appBuilder.Build();
            var model = app.Services.GetRequiredService<DistributedApplicationModel>();

            var resource = Assert.Single(model.Resources.OfType<K8sManifestResource>());
            var typeSnapshot = resource.Annotations
                .OfType<ResourceSnapshotAnnotation>()
                .Select(a => a.InitialSnapshot.ResourceType)
                .FirstOrDefault();

            Assert.Equal("K8s Kustomize", typeSnapshot);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
