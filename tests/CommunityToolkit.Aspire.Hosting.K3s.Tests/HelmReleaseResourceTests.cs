using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class HelmReleaseResourceTests
{
    [Fact]
    public void AddHelmReleaseAddsHelmReleaseResourceWithCorrectName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal("argocd", resource.Name);
    }

    [Fact]
    public void HelmReleaseResourceIsContainerResource()
    {
        // HelmReleaseResource extends ContainerResource — it runs bitnami/helm in Docker.
        // No host-side helm binary required. WaitForCompletion waits for exit code 0.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.IsAssignableFrom<ContainerResource>(resource);
    }

    [Fact]
    public void AddHelmReleaseDefaultsReleaseName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("my-release", "my-chart");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal("my-release", resource.ReleaseName);
    }

    [Fact]
    public void AddHelmReleaseDefaultsNamespace()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal("default", resource.Namespace);
    }

    [Fact]
    public void AddHelmReleaseWithExplicitNamespace()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd", @namespace: "argocd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal("argocd", resource.Namespace);
    }

    [Fact]
    public void AddHelmReleaseStoresRepoUrl()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease(
            "argocd", "argo-cd",
            repo: "https://argoproj.github.io/argo-helm");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal("https://argoproj.github.io/argo-helm", resource.RepoUrl);
    }

    [Fact]
    public void AddHelmReleaseStoresVersion()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd", version: "7.8.0");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal("7.8.0", resource.Version);
    }

    [Fact]
    public void HelmReleaseParentIsCluster()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Same(cluster.Resource, resource.Parent);
        Assert.IsAssignableFrom<IResourceWithParent<K3sClusterResource>>(resource);
    }

    [Fact]
    public void WithHelmValueAccumulatesValues()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd")
            .WithHelmValue("server.service.type", "NodePort")
            .WithHelmValue("server.insecure", "true");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal("NodePort", resource.HelmValues["server.service.type"]);
        Assert.Equal("true", resource.HelmValues["server.insecure"]);
    }

    [Fact]
    public void AddServiceEndpointAddsEndpointResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        cluster.AddHelmRelease("argocd", "argo-cd");

        cluster.AddServiceEndpoint("argocd-ui", "argocd-server", servicePort: 443, @namespace: "argocd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var ep = Assert.Single(model.Resources.OfType<K3sServiceEndpointResource>());
        Assert.Equal("argocd-server", ep.ServiceName);
        Assert.Equal(443, ep.ServicePort);
        Assert.Equal("argocd", ep.Namespace);
    }

    [Fact]
    public void AddServiceEndpointMultipleEndpointsAllRegistered()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddServiceEndpoint("ui", "argocd-server", 443);
        cluster.AddServiceEndpoint("http", "argocd-server", 80);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Equal(2, model.Resources.OfType<K3sServiceEndpointResource>().Count());
    }

    [Fact]
    public void HelmReleaseIsExcludedFromManifest()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, resource.Annotations);
    }

    // ── BuildHelmScript tests (pure logic, no DI needed) ──────────────────────

    private static HelmReleaseResource MakeRelease(
        string releaseName, string chart, string? repo, string? version,
        string @namespace, Dictionary<string, string>? values = null)
    {
        var cluster = new K3sClusterResource("k8s");
        var r = new HelmReleaseResource(releaseName, releaseName, @namespace, cluster)
        {
            Chart = chart,
            RepoUrl = repo,
            Version = version,
        };
        foreach (var kv in values ?? [])
            r.HelmValues[kv.Key] = kv.Value;
        return r;
    }

    [Fact]
    public void BuildHelmScriptIncludesUpgradeInstall()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("argocd", "argo-cd", null, null, "argocd"));

        Assert.Contains("helm upgrade --install", script);
        Assert.Contains("\"argocd\"", script);
        Assert.Contains("\"argo-cd\"", script);
    }

    [Fact]
    public void BuildHelmScriptIncludesWaitAndNamespace()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "chart", null, null, "my-ns"));

        Assert.Contains("--wait", script);
        Assert.Contains("--namespace \"my-ns\"", script);
        Assert.Contains("--create-namespace", script);
    }

    [Fact]
    public void BuildHelmScriptWithRepoAddsRepoSteps()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "chart", "https://my-repo.example.com", null, "default"));

        Assert.Contains("helm repo add", script);
        Assert.Contains("helm repo update", script);
        Assert.Contains("aspire-k3s-r/chart", script);
    }

    [Fact]
    public void BuildHelmScriptWithoutRepoSkipsRepoSteps()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "oci://registry/chart", null, null, "default"));

        Assert.DoesNotContain("helm repo add", script);
        Assert.Contains("\"oci://registry/chart\"", script);
    }

    [Fact]
    public void BuildHelmScriptIncludesVersion()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "chart", null, "7.8.0", "default"));

        Assert.Contains("--version \"7.8.0\"", script);
    }

    [Fact]
    public void BuildHelmScriptOmitsVersionWhenNull()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "chart", null, null, "default"));

        Assert.DoesNotContain("--version", script);
    }

    [Fact]
    public void BuildHelmScriptIncludesSetValues()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "chart", null, null, "default", new()
            {
                ["service.type"] = "NodePort",
                ["replicaCount"] = "2",
            }));

        Assert.Contains("--set \"service.type=NodePort\"", script);
        Assert.Contains("--set \"replicaCount=2\"", script);
    }

    // ── WaitForCompletion support ─────────────────────────────────────────────

    [Fact]
    public void HelmReleaseHasNoHealthCheckAnnotation()
    {
        // HelmReleaseResource is a run-to-completion container — consumers use
        // WaitForCompletion(helmRelease) rather than WaitFor.  No health check needed.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Empty(resource.Annotations.OfType<HealthCheckAnnotation>());
    }

    // ── Public API null-guard tests ───────────────────────────────────────────

    [Fact]
    public void AddHelmReleaseShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;
        var action = () => builder.AddHelmRelease("argocd", "argo-cd");
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddHelmReleaseShouldThrowWhenNameIsNull()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var action = () => cluster.AddHelmRelease(null!, "argo-cd");
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddHelmReleaseShouldThrowWhenChartIsNull()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var action = () => cluster.AddHelmRelease("argocd", null!);
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void WithHelmValueShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<HelmReleaseResource> builder = null!;
        var action = () => builder.WithHelmValue("key", "value");
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void AddServiceEndpointShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K3sClusterResource> builder = null!;
        var action = () => builder.AddServiceEndpoint("ui", "svc", 443);
        Assert.Throws<ArgumentNullException>(action);
    }

    // ── WithHelmValuesFile tests ──────────────────────────────────────────────

    [Fact]
    public void BuildHelmScriptIncludesValuesFiles()
    {
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("argocd", "argocd", "argocd", cluster)
        {
            Chart = "argo-cd",
        };
        release.ValuesFiles.Add("/tmp/values.yaml");
        release.ValuesFiles.Add("/tmp/values-prod.yaml");

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        Assert.Contains("--values \"/helm-values/values.yaml\"", script);
        Assert.Contains("--values \"/helm-values/values-prod.yaml\"", script);
        // Values files are applied before --set overrides (last wins).
        var valuesIndex = script.IndexOf("--values", StringComparison.Ordinal);
        var setIndex = script.IndexOf("--set", StringComparison.Ordinal);
        Assert.True(valuesIndex < setIndex || setIndex == -1,
            "--values flags should appear before --set flags");
    }

    [Fact]
    public void WithHelmValuesFileAccumulatesAbsolutePaths()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        // Use a temp file so the path resolution succeeds.
        var tempFile = Path.Combine(appBuilder.AppHostDirectory, "values.yaml");

        cluster.AddHelmRelease("argocd", "argo-cd")
            .WithHelmValuesFile("values.yaml");  // relative to AppHostDirectory

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Single(resource.ValuesFiles);
        Assert.Equal(tempFile, resource.ValuesFiles[0]);
    }

    [Fact]
    public void WithHelmValuesFileShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<HelmReleaseResource> builder = null!;
        var action = () => builder.WithHelmValuesFile("values.yaml");
        Assert.Throws<ArgumentNullException>(action);
    }
}
