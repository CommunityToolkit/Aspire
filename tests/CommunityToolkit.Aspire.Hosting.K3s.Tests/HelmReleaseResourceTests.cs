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
        Assert.Contains("'argocd'", script);
        Assert.Contains("'argo-cd'", script);
    }

    [Fact]
    public void BuildHelmScriptIncludesWaitAndNamespace()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "chart", null, null, "my-ns"));

        Assert.Contains("--wait", script);
        Assert.Contains("--namespace 'my-ns'", script);
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
        Assert.Contains("'oci://registry/chart'", script);
    }

    [Fact]
    public void BuildHelmScriptIncludesVersion()
    {
        var script = K3sHelmBuilderExtensions.BuildHelmScript(
            MakeRelease("r", "chart", null, "7.8.0", "default"));

        Assert.Contains("--version '7.8.0'", script);
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

        Assert.Contains("--set 'service.type=NodePort'", script);
        Assert.Contains("--set 'replicaCount=2'", script);
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
    public void BuildHelmScriptIncludesValuesFilesWithIndexPrefix()
    {
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("argocd", "argocd", "argocd", cluster)
        {
            Chart = "argo-cd",
        };
        release.ValuesFiles.Add("/tmp/values.yaml");
        release.ValuesFiles.Add("/tmp/values-prod.yaml");

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        // Index prefix guarantees uniqueness even when basenames collide.
        Assert.Contains("--values '/helm-values/0-values.yaml'", script);
        Assert.Contains("--values '/helm-values/1-values-prod.yaml'", script);
    }

    [Fact]
    public void BuildHelmScriptValuesFilesOrderedBeforeSetFlags()
    {
        // Helm precedence: --values (ascending index) → --set (highest, always wins).
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("argocd", "argocd", "argocd", cluster)
        {
            Chart = "argo-cd",
        };
        release.ValuesFiles.Add("/tmp/base.yaml");
        release.ValuesFiles.Add("/tmp/prod.yaml");
        release.HelmValues["key"] = "override";

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        var firstValuesIdx = script.IndexOf("--values '/helm-values/0-", StringComparison.Ordinal);
        var secondValuesIdx = script.IndexOf("--values '/helm-values/1-", StringComparison.Ordinal);
        var setIdx = script.IndexOf("--set", StringComparison.Ordinal);

        Assert.True(firstValuesIdx < secondValuesIdx, "0-base.yaml must precede 1-prod.yaml");
        Assert.True(secondValuesIdx < setIdx, "--values flags must precede --set flags");
    }

    [Fact]
    public void BuildHelmScriptCollisionSafeWithSameBasename()
    {
        // Two files from different directories with the same name must not collide.
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("r", "r", "default", cluster) { Chart = "chart" };
        release.ValuesFiles.Add("/prod/values.yaml");
        release.ValuesFiles.Add("/dev/values.yaml");

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        Assert.Contains("--values '/helm-values/0-values.yaml'", script);
        Assert.Contains("--values '/helm-values/1-values.yaml'", script);
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

    // ── WithHelmValue override ────────────────────────────────────────────────

    [Fact]
    public void WithHelmValueOverridesDuplicateKey()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd")
            .WithHelmValue("replicaCount", "1")
            .WithHelmValue("replicaCount", "3");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());

        // Dictionary last-write wins — final value must be "3".
        Assert.Equal("3", resource.HelmValues["replicaCount"]);
    }

    [Fact]
    public void BuildHelmScriptDuplicateKeyAppearsOnce()
    {
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("r", "r", "default", cluster) { Chart = "chart" };
        release.HelmValues["replicaCount"] = "3";

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        // Dictionary deduplication: key appears exactly once in the --set flags.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, "replicaCount").Cast<System.Text.RegularExpressions.Match>());
    }

    // ── HelmEscape / ShellEscape via BuildHelmScript ──────────────────────────

    [Fact]
    public void BuildHelmScriptHelmEscapesCommaInValue()
    {
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("r", "r", "default", cluster) { Chart = "chart" };
        release.HelmValues["tags"] = "a,b,c";

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        // Helm --set comma is a list delimiter; must be backslash-escaped.
        Assert.Contains(@"a\,b\,c", script);
    }

    [Fact]
    public void BuildHelmScriptHelmEscapesOpenBraceInValue()
    {
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("r", "r", "default", cluster) { Chart = "chart" };
        release.HelmValues["config"] = "{key:val}";

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        Assert.Contains(@"\{key:val\}", script);
    }

    [Fact]
    public void BuildHelmScriptHelmEscapesBackslashInValue()
    {
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("r", "r", "default", cluster) { Chart = "chart" };
        release.HelmValues["path"] = @"C:\data";

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        // Backslash must be doubled so Helm does not treat it as an escape prefix.
        Assert.Contains(@"C:\\data", script);
    }

    [Fact]
    public void BuildHelmScriptShellEscapesSingleQuoteInValue()
    {
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("r", "r", "default", cluster) { Chart = "chart" };
        release.HelmValues["msg"] = "it's";

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        // POSIX single-quote escape: ' → '\''
        Assert.Contains("it'\\''s", script);
    }

    [Fact]
    public void BuildHelmScriptHelmEscapesCommaInKey()
    {
        // Helm --set parser applies the same metacharacter rules to keys.
        var cluster = new K3sClusterResource("k8s");
        var release = new HelmReleaseResource("r", "r", "default", cluster) { Chart = "chart" };
        release.HelmValues["a,b"] = "v";

        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        Assert.Contains(@"a\,b=v", script);
    }

    // ── WithHelmValuesFile with absolute path ─────────────────────────────────

    [Fact]
    public void WithHelmValuesFileStoresAbsolutePathAsIs()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        var absolutePath = Path.Combine(Path.GetTempPath(), "values.yaml");

        cluster.AddHelmRelease("argocd", "argo-cd")
            .WithHelmValuesFile(absolutePath);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        var file = Assert.Single(resource.ValuesFiles);
        Assert.Equal(absolutePath, file);
    }

    [Fact]
    public void WithHelmValuesFileMultipleFilesAccumulate()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd")
            .WithHelmValuesFile("base.yaml")
            .WithHelmValuesFile("prod.yaml");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal(2, resource.ValuesFiles.Count);
    }

    // ── Cluster HelmReleases tracking ─────────────────────────────────────────

    [Fact]
    public void ClusterTracksRegisteredHelmReleases()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd");
        cluster.AddHelmRelease("cert-manager", "cert-manager");

        Assert.Contains("argocd", cluster.Resource.HelmReleases.Keys);
        Assert.Contains("cert-manager", cluster.Resource.HelmReleases.Keys);
        Assert.Equal(2, cluster.Resource.HelmReleases.Count);
    }

    // ── Service endpoint from a different cluster not included ────────────────

    [Fact]
    public void ServiceEndpointParentMatchesItsCluster()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var clusterA = appBuilder.AddK3sCluster("k8s-a");
        var clusterB = appBuilder.AddK3sCluster("k8s-b");

        clusterA.AddServiceEndpoint("ep-a", "svc", 80);
        clusterB.AddServiceEndpoint("ep-b", "svc", 80);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var eps = model.Resources.OfType<K3sServiceEndpointResource>().ToList();
        Assert.Equal(2, eps.Count);

        Assert.Same(clusterA.Resource, eps.Single(e => e.Name == "ep-a").Parent);
        Assert.Same(clusterB.Resource, eps.Single(e => e.Name == "ep-b").Parent);
    }
}
