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
    public void AddHelmReleaseDoesNotCreateSeparateInstallResource()
    {
        // helm upgrade --install runs internally inside the HelmReleaseResource lifecycle;
        // no separate ExecutableResource is added to the application model.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Empty(model.Resources.OfType<ExecutableResource>());
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
    }

    [Fact]
    public void HelmReleaseImplementsNonGenericIResourceWithParent()
    {
        // The Aspire dashboard uses the non-generic IResourceWithParent to group
        // child resources under their parent. Verify both the generic and non-generic
        // interfaces are satisfied and point to the same cluster resource.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());

        // Non-generic IResourceWithParent (used by the dashboard)
        var nonGeneric = resource as IResourceWithParent;
        Assert.NotNull(nonGeneric);
        Assert.Same(cluster.Resource, nonGeneric.Parent);
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
    public void WithEndpointAccumulatesEndpoints()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd")
            .WithEndpoint("argocd-server", servicePort: 443, name: "ui");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        var ep = Assert.Single(resource.EndpointDefinitions);
        Assert.Equal("argocd-server", ep.ServiceName);
        Assert.Equal(443, ep.ServicePort);
        Assert.Equal("ui", ep.EndpointName);
    }

    [Fact]
    public void WithEndpointMultipleEndpoints()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        cluster.AddHelmRelease("argocd", "argo-cd")
            .WithEndpoint("argocd-server", 443, "ui")
            .WithEndpoint("argocd-server", 80, "http");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Equal(2, resource.EndpointDefinitions.Count);
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

    // ── BuildHelmInstallArgs tests (pure logic, no DI needed) ─────────────────

    [Fact]
    public void BuildHelmInstallArgsIncludesUpgradeInstall()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "argocd", "argo-cd", null, null, "argocd", null, "/tmp/admin.yaml");

        var list = args.ToArray();
        Assert.Contains("upgrade", list);
        Assert.Contains("--install", list);
        Assert.Contains("argocd", list);
        Assert.Contains("argo-cd", list);
    }

    [Fact]
    public void BuildHelmInstallArgsIncludesKubeconfig()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", null, null, "default", null, "/tmp/admin.yaml");

        Assert.Contains("--kubeconfig=/tmp/admin.yaml", args);
    }

    [Fact]
    public void BuildHelmInstallArgsIncludesWait()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", null, null, "default", null, "/tmp/admin.yaml");

        Assert.Contains("--wait", args);
    }

    [Fact]
    public void BuildHelmInstallArgsIncludesNamespace()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", null, null, "my-ns", null, "/tmp/admin.yaml");

        var list = args.ToArray();
        Assert.Contains("--namespace", list);
        Assert.Contains("my-ns", list);
        Assert.Contains("--create-namespace", list);
    }

    [Fact]
    public void BuildHelmInstallArgsWithRepoAliasUsesPrefixedChartRef()
    {
        // When a repo alias is pre-registered via `helm repo add`, BuildHelmInstallArgs
        // uses "{alias}/{chart}" notation — NOT the --repo flag (which is unreliable).
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", "my-repo-alias", null, "default", null, "/tmp/admin.yaml");

        var list = args.ToArray();
        Assert.DoesNotContain("--repo", list);
        Assert.Contains("my-repo-alias/chart", list);
    }

    [Fact]
    public void BuildHelmInstallArgsWithNullAliasUsesChartDirectly()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "oci://registry/chart", null, null, "default", null, "/tmp/admin.yaml");

        Assert.DoesNotContain("--repo", args);
        Assert.Contains("oci://registry/chart", args);
    }

    [Fact]
    public void BuildHelmInstallArgsIncludesVersion()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", null, "7.8.0", "default", null, "/tmp/admin.yaml");

        var list = args.ToArray();
        Assert.Contains("--version", list);
        Assert.Contains("7.8.0", list);
    }

    [Fact]
    public void BuildHelmInstallArgsOmitsRepoWhenNull()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", null, null, "default", null, "/tmp/admin.yaml");

        Assert.DoesNotContain("--repo", args);
    }

    [Fact]
    public void BuildHelmInstallArgsOmitsVersionWhenNull()
    {
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", null, null, "default", null, "/tmp/admin.yaml");

        Assert.DoesNotContain("--version", args);
    }

    [Fact]
    public void BuildHelmInstallArgsIncludesSetValues()
    {
        var values = new Dictionary<string, string>
        {
            ["service.type"] = "NodePort",
            ["replicaCount"] = "2",
        };
        var args = K3sHelmBuilderExtensions.BuildHelmInstallArgs(
            "r", "chart", null, null, "default", values, "/tmp/admin.yaml");

        var list = args.ToArray();
        Assert.Contains("--set", list);
        Assert.Contains("service.type=NodePort", list);
        Assert.Contains("replicaCount=2", list);
    }

    // ── WaitFor support ───────────────────────────────────────────────────────

    [Fact]
    public void HelmReleaseHasHealthCheckForWaitForSupport()
    {
        // WaitFor(helmRelease) is satisfied by the HelmReleaseHealthCheck,
        // which flips IsReady once RunReleaseAsync completes.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        cluster.AddHelmRelease("argocd", "argo-cd");

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(model.Resources.OfType<HelmReleaseResource>());
        Assert.Contains(resource.Annotations.OfType<HealthCheckAnnotation>(), a =>
            a.Key == "helm_argocd_ready");
    }

    [Fact]
    public void HelmReleaseIsReadyFlagStartsFalse()
    {
        var resource = new HelmReleaseResource(
            "argocd", "argocd", "default", new K3sClusterResource("k8s"));

        Assert.False(resource.IsReady);
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
    public void WithEndpointShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<HelmReleaseResource> builder = null!;
        var action = () => builder.WithEndpoint("svc", 443, "ui");
        Assert.Throws<ArgumentNullException>(action);
    }
}
