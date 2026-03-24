using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindClusterResourceTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var resource = new KindClusterResource("my-resource", "my-cluster", "/tmp/kubeconfig.yaml");

        Assert.Equal("my-resource", resource.Name);
        Assert.Equal("my-cluster", resource.ClusterName);
        Assert.Equal("/tmp/kubeconfig.yaml", resource.KubeconfigPath);
    }

    [Fact]
    public void Constructor_DefaultsAreCorrect()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");

        Assert.Equal(0, resource.NodeCount);
        Assert.Null(resource.KubernetesVersion);
        Assert.Null(resource.ConfigPath);
        Assert.Equal(TimeSpan.FromMinutes(5), resource.ReadyTimeout);
        Assert.Empty(resource.PortMappings);
        Assert.Empty(resource.HelmCharts);
        Assert.Empty(resource.ManifestPaths);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsOnEmptyClusterName(string clusterName)
    {
        Assert.Throws<ArgumentException>(() => new KindClusterResource("name", clusterName, "/tmp/kc.yaml"));
    }

    [Theory]
    [InlineData("UPPERCASE")]
    [InlineData("has space")]
    [InlineData("-starts-with-hyphen")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    public void Constructor_ThrowsOnInvalidClusterName(string clusterName)
    {
        var ex = Assert.Throws<ArgumentException>(() => new KindClusterResource("name", clusterName, "/tmp/kc.yaml"));
        Assert.Contains("lowercase", ex.Message);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("valid-name")]
    [InlineData("v123")]
    [InlineData("my-cluster-1")]
    public void Constructor_AcceptsValidClusterNames(string clusterName)
    {
        var resource = new KindClusterResource("name", clusterName, "/tmp/kc.yaml");
        Assert.Equal(clusterName, resource.ClusterName);
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyKubeconfigPath()
    {
        Assert.Throws<ArgumentException>(() => new KindClusterResource("name", "cluster", ""));
    }

    [Fact]
    public void ConnectionStringExpression_ReturnsKubeconfigPath()
    {
        var resource = new KindClusterResource("r", "cluster", "/home/user/.kube/kind.yaml");
        Assert.NotNull(resource.ConnectionStringExpression);
    }

    [Fact]
    public void AddPortMapping_AppearsInPortMappings()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");
        resource.AddPortMapping(new KindPortMapping(8080, 80));

        var mapping = Assert.Single(resource.PortMappings);
        Assert.Equal(8080, mapping.HostPort);
        Assert.Equal(80, mapping.ContainerPort);
        Assert.Equal("TCP", mapping.Protocol);
    }

    [Fact]
    public void AddHelmChart_AppearsInHelmCharts()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");
        var chart = new KindHelmChart("nginx", "ingress-nginx/ingress-nginx");
        resource.AddHelmChart(chart);

        Assert.Single(resource.HelmCharts, h => h == chart);
    }

    [Fact]
    public void AddManifestPath_AppearsInManifestPaths()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");
        resource.AddManifestPath("/k8s/deployment.yaml");

        Assert.Single(resource.ManifestPaths, "/k8s/deployment.yaml");
    }
}

public class KindClusterBuilderExtensionsTests
{
    [Fact]
    public void AddKindCluster_RegistersResourceWithCorrectName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("dev-cluster");

        Assert.Equal("dev-cluster", rb.Resource.Name);
    }

    [Fact]
    public void AddKindCluster_UsesNameAsClusterNameByDefault()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("my-cluster");

        Assert.Equal("my-cluster", rb.Resource.ClusterName);
    }

    [Fact]
    public void AddKindCluster_AcceptsExplicitClusterName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("aspire-name", clusterName: "kind-name");

        Assert.Equal("kind-name", rb.Resource.ClusterName);
    }

    [Fact]
    public void AddKindCluster_AcceptsExplicitKubeconfigPath()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c", kubeconfigPath: "/custom/kc.yaml");

        Assert.Equal("/custom/kc.yaml", rb.Resource.KubeconfigPath);
    }

    [Fact]
    public void AddKindCluster_GeneratesDefaultKubeconfigPathInTempDir()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("my-cluster");

        Assert.StartsWith(Path.GetTempPath(), rb.Resource.KubeconfigPath);
        Assert.Contains("my-cluster", rb.Resource.KubeconfigPath);
        Assert.EndsWith(".yaml", rb.Resource.KubeconfigPath);
    }

    [Fact]
    public void AddKindCluster_ThrowsOnNullBuilder()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddKindCluster("cluster"));
    }

    [Fact]
    public void AddKindCluster_ThrowsOnEmptyName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentException>(() => appBuilder.AddKindCluster(string.Empty));
    }

    [Fact]
    public void WithNodeCount_SetsNodeCount()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c").WithNodeCount(3);

        Assert.Equal(3, rb.Resource.NodeCount);
    }

    [Fact]
    public void WithNodeCount_ThrowsOnNegativeValue()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c");

        Assert.Throws<ArgumentOutOfRangeException>(() => rb.WithNodeCount(-1));
    }

    [Fact]
    public void WithKubernetesVersion_SetsVersion()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c").WithKubernetesVersion("v1.31.0");

        Assert.Equal("v1.31.0", rb.Resource.KubernetesVersion);
    }

    [Fact]
    public void WithConfig_SetsConfigPath()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c").WithConfig("/path/to/kind-config.yaml");

        Assert.Equal("/path/to/kind-config.yaml", rb.Resource.ConfigPath);
    }

    [Fact]
    public void WithPortMapping_AddsPortMapping()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c").WithPortMapping(8080, 80);

        var mapping = Assert.Single(rb.Resource.PortMappings);
        Assert.Equal(8080, mapping.HostPort);
        Assert.Equal(80, mapping.ContainerPort);
        Assert.Equal("TCP", mapping.Protocol);
    }

    [Fact]
    public void WithPortMapping_RespectsProtocol()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c").WithPortMapping(5353, 53, "UDP");

        var mapping = Assert.Single(rb.Resource.PortMappings);
        Assert.Equal("UDP", mapping.Protocol);
    }

    [Fact]
    public void WithHelmChart_AddsHelmChart()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c")
            .WithHelmChart("nginx", "ingress-nginx/ingress-nginx", "ingress-nginx");

        var chart = Assert.Single(rb.Resource.HelmCharts);
        Assert.Equal("nginx", chart.ReleaseName);
        Assert.Equal("ingress-nginx/ingress-nginx", chart.Chart);
        Assert.Equal("ingress-nginx", chart.Namespace);
    }

    [Fact]
    public void WithManifest_AddsManifestPath()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c").WithManifest("/k8s/namespace.yaml");

        Assert.Single(rb.Resource.ManifestPaths, "/k8s/namespace.yaml");
    }

    [Fact]
    public void WithWaitForReady_SetsTimeout()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c").WithWaitForReady(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), rb.Resource.ReadyTimeout);
    }

    [Fact]
    public void WithWaitForReady_ThrowsOnZeroTimeout()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c");

        Assert.Throws<ArgumentOutOfRangeException>(() => rb.WithWaitForReady(TimeSpan.Zero));
    }

    [Fact]
    public void WithWaitForReady_ThrowsOnNegativeTimeout()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c");

        Assert.Throws<ArgumentOutOfRangeException>(() => rb.WithWaitForReady(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void FluentApi_AllMethodsReturnSameBuilder()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rb = appBuilder.AddKindCluster("c");

        Assert.Same(rb, rb.WithNodeCount(2));
        Assert.Same(rb, rb.WithKubernetesVersion("v1.31.0"));
        Assert.Same(rb, rb.WithConfig("/cfg.yaml"));
        Assert.Same(rb, rb.WithPortMapping(80, 80));
        Assert.Same(rb, rb.WithHelmChart("r", "chart"));
        Assert.Same(rb, rb.WithManifest("/m.yaml"));
        Assert.Same(rb, rb.WithWaitForReady(TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void AddKindCluster_CalledTwice_RegistersOnlyOneLifecycleHook()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddKindCluster("cluster-a");
        appBuilder.AddKindCluster("cluster-b");

        using var app = appBuilder.Build();
        var hooks = app.Services
            .GetServices<global::Aspire.Hosting.Lifecycle.IDistributedApplicationLifecycleHook>()
            .OfType<global::Aspire.Hosting.KindClusterLifecycleHook>();

        Assert.Single(hooks);
    }
}

public class KindClusterConfigGenerationTests
{
    [Fact]
    public void GenerateKindConfig_MinimalCluster_HasControlPlaneOnly()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");

        var yaml = global::Aspire.Hosting.KindClusterLifecycleHook.GenerateKindConfig(resource);

        Assert.Contains("kind: Cluster", yaml);
        Assert.Contains("apiVersion: kind.x-k8s.io/v1alpha4", yaml);
        Assert.Contains("role: control-plane", yaml);
        Assert.DoesNotContain("role: worker", yaml);
        Assert.DoesNotContain("image:", yaml);
        Assert.DoesNotContain("extraPortMappings:", yaml);
    }

    [Fact]
    public void GenerateKindConfig_WithWorkers_IncludesWorkerNodes()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");
        resource.NodeCount = 2;

        var yaml = global::Aspire.Hosting.KindClusterLifecycleHook.GenerateKindConfig(resource);

        Assert.Equal(2, yaml.Split('\n').Count(l => l.Contains("role: worker")));
    }

    [Fact]
    public void GenerateKindConfig_WithKubernetesVersion_IncludesImageOnAllNodes()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");
        resource.KubernetesVersion = "v1.31.0";
        resource.NodeCount = 1;

        var yaml = global::Aspire.Hosting.KindClusterLifecycleHook.GenerateKindConfig(resource);

        Assert.Contains("image: kindest/node:v1.31.0", yaml);
        Assert.Equal(2, yaml.Split('\n').Count(l => l.Contains("kindest/node:v1.31.0")));
    }

    [Fact]
    public void GenerateKindConfig_WithPortMappings_IncludesExtraPortMappings()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");
        resource.AddPortMapping(new KindPortMapping(8080, 80, "TCP"));
        resource.AddPortMapping(new KindPortMapping(5353, 53, "UDP"));

        var yaml = global::Aspire.Hosting.KindClusterLifecycleHook.GenerateKindConfig(resource);

        Assert.Contains("extraPortMappings:", yaml);
        Assert.Contains("containerPort: 80", yaml);
        Assert.Contains("hostPort: 8080", yaml);
        Assert.Contains("protocol: TCP", yaml);
        Assert.Contains("containerPort: 53", yaml);
        Assert.Contains("hostPort: 5353", yaml);
        Assert.Contains("protocol: UDP", yaml);
    }

    [Fact]
    public void GenerateKindConfig_ValidYamlIndentation()
    {
        var resource = new KindClusterResource("r", "c", "/tmp/kc.yaml");
        resource.NodeCount = 1;
        resource.KubernetesVersion = "v1.30.0";
        resource.AddPortMapping(new KindPortMapping(80, 80));

        var yaml = global::Aspire.Hosting.KindClusterLifecycleHook.GenerateKindConfig(resource);
        var lines = yaml.Split('\n');
        var cpIndex = Array.FindIndex(lines, l => l.Contains("role: control-plane"));
        var imageIndex = Array.FindIndex(lines, l => l.Contains("image: kindest/node:v1.30.0"));

        Assert.True(imageIndex > cpIndex, "image line should appear after control-plane declaration");
    }
}
