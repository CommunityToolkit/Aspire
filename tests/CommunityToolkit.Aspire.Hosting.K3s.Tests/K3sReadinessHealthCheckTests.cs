using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.Autorest;
using k8s.KubeConfigModels;
using k8s.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using System.Net;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sReadinessHealthCheckTests
{
    // ── CheckHealthAsync — early exit (no DCP allocation) ────────────────────

    [Fact]
    public async Task CheckHealthAsync_WhenNeitherAllocatedEndpointNorStaticPort_ReturnsUnhealthy()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");
        // No DCP, no static port — both annotation.AllocatedEndpoint and annotation.Port are null.
        var healthCheck = new K3sReadinessHealthCheck(cluster.Resource);

        var result = await healthCheck.CheckHealthAsync(null!);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not yet allocated", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllocatedEndpointSet_ProceedsToCheckCore()
    {
        // Simulates DCP having fired the endpoint allocation event.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        var annotation = cluster.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .First(a => a.Name == K3sClusterResource.ApiServerEndpointName);
        // Set AllocatedEndpoint directly — the same property DCP sets when it allocates the port.
        annotation.AllocatedEndpoint = new AllocatedEndpoint(annotation, "localhost", 32773);

        var healthCheck = new K3sReadinessHealthCheck(cluster.Resource);

        var result = await healthCheck.CheckHealthAsync(null!);

        // cluster/kubeconfig.yaml doesn't exist in the test dir → reaches CheckCoreAsync
        // and returns "write kubeconfig", not "not yet allocated".
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("not yet allocated", result.Description);
        Assert.Contains("write kubeconfig", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStaticPortConfigured_ProceedsToCheckCore()
    {
        // Simulates AddK3sCluster("k8s", apiServerPort: 32773): AllocatedEndpoint is null
        // but annotation.Port carries the explicit static port.
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        var annotation = cluster.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .First(a => a.Name == K3sClusterResource.ApiServerEndpointName);
        annotation.Port = 32773;

        var healthCheck = new K3sReadinessHealthCheck(cluster.Resource);

        var result = await healthCheck.CheckHealthAsync(null!);

        // cluster/kubeconfig.yaml doesn't exist → "write kubeconfig", not "not yet allocated".
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("not yet allocated", result.Description);
        Assert.Contains("write kubeconfig", result.Description);
    }

    // ── CheckCoreAsync — file-system and Kubernetes paths ────────────────────

    [Fact]
    public async Task CheckCoreAsync_WhenKubeconfigDirectoryIsNull_ReturnsUnhealthy()
    {
        var cluster = new K3sClusterResource("k8s") { KubeconfigDirectory = null };
        var healthCheck = new K3sReadinessHealthCheck(cluster, _ => Mock.Of<IKubernetes>());

        var result = await healthCheck.CheckCoreAsync(port: 6443);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not configured", result.Description);
    }

    [Fact]
    public async Task CheckCoreAsync_WhenKubeconfigFileMissing_ReturnsUnhealthy()
    {
        var (healthCheck, _, _) = MakeCheck(writeKubeconfig: false, nodeCount: 0, agentCount: 0);

        var result = await healthCheck.CheckCoreAsync(port: 6443);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("write kubeconfig", result.Description);
    }

    [Fact]
    public async Task CheckCoreAsync_WhenAllNodesReady_ReturnsHealthy()
    {
        var (healthCheck, _, _) = MakeCheck(writeKubeconfig: true, nodeCount: 1, agentCount: 0);

        var result = await healthCheck.CheckCoreAsync(port: 6443);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("ready", result.Description);
    }

    [Fact]
    public async Task CheckCoreAsync_WhenNodesNotReady_ReturnsUnhealthy()
    {
        var (healthCheck, _, _) = MakeCheck(writeKubeconfig: true, nodeCount: 0, agentCount: 0);

        var result = await healthCheck.CheckCoreAsync(port: 6443);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("0/1 nodes Ready", result.Description);
    }

    [Fact]
    public async Task CheckCoreAsync_WithAgentsRequiresAllNodes()
    {
        // 1 server + 2 agents = 3 required; only 1 node ready → unhealthy.
        var (healthCheck, _, _) = MakeCheck(writeKubeconfig: true, nodeCount: 1, agentCount: 2);

        var result = await healthCheck.CheckCoreAsync(port: 6443);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("1/3", result.Description);
    }

    [Fact]
    public async Task CheckCoreAsync_WhenUnexpectedExceptionThrown_ReturnsUnhealthy()
    {
        var (healthCheck, _, _) = MakeCheck(
            writeKubeconfig: true,
            nodeCount: 0,
            agentCount: 0,
            listNodesThrows: new InvalidOperationException("cluster exploded"));

        var result = await healthCheck.CheckCoreAsync(port: 6443);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("cluster exploded", result.Description);
    }

    [Fact]
    public async Task CheckCoreAsync_WhenTlsFailure_ReturnsStaleKubeconfigMessage()
    {
        var (healthCheck, dir, _) = MakeCheck(
            writeKubeconfig: true,
            nodeCount: 0,
            agentCount: 0,
            listNodesThrows: new System.Security.Authentication.AuthenticationException("bad cert"));

        var result = await healthCheck.CheckCoreAsync(port: 6443);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("stale", result.Description);

        // The raw cluster kubeconfig must NOT be deleted — only the derived variants.
        Assert.True(File.Exists(Path.Combine(dir, "cluster", "kubeconfig.yaml")));
    }

    [Fact]
    public async Task CheckCoreAsync_WritesLocalAndContainerKubeconfigVariants()
    {
        var (healthCheck, dir, _) = MakeCheck(writeKubeconfig: true, nodeCount: 1, agentCount: 0);

        await healthCheck.CheckCoreAsync(port: 6443);

        Assert.True(File.Exists(Path.Combine(dir, "local", "kubeconfig.yaml")));
        Assert.True(File.Exists(Path.Combine(dir, "container", "kubeconfig.yaml")));
    }

    [Fact]
    public async Task CheckCoreAsync_LocalVariantContainsLocalhostUrl()
    {
        var (healthCheck, dir, _) = MakeCheck(writeKubeconfig: true, nodeCount: 1, agentCount: 0);

        await healthCheck.CheckCoreAsync(port: 7777);

        var localYaml = await File.ReadAllTextAsync(Path.Combine(dir, "local", "kubeconfig.yaml"));
        Assert.Contains("https://localhost:7777", localYaml);
    }

    [Fact]
    public async Task CheckCoreAsync_ContainerVariantContainsClusterNameUrl()
    {
        var (healthCheck, dir, _) = MakeCheck(writeKubeconfig: true, nodeCount: 1, agentCount: 0);

        await healthCheck.CheckCoreAsync(port: 6443);

        var containerYaml = await File.ReadAllTextAsync(Path.Combine(dir, "container", "kubeconfig.yaml"));
        Assert.Contains("https://k8s:6443", containerYaml);
    }

    // ── BuildConfigYaml ───────────────────────────────────────────────────────

    [Fact]
    public void BuildConfigYaml_RewritesServerUrl()
    {
        var source = new K8SConfiguration
        {
            Clusters =
            [
                new Cluster { Name = "k8s", ClusterEndpoint = new ClusterEndpoint { Server = "https://original:6443" } }
            ]
        };

        var yaml = K3sReadinessHealthCheck.BuildConfigYaml(source, "https://localhost:9999");

        Assert.Contains("https://localhost:9999", yaml);
        Assert.DoesNotContain("https://original:6443", yaml);
    }

    [Fact]
    public void BuildConfigYaml_RewritesAllClusters()
    {
        var source = new K8SConfiguration
        {
            Clusters =
            [
                new Cluster { Name = "a", ClusterEndpoint = new ClusterEndpoint { Server = "https://a:6443" } },
                new Cluster { Name = "b", ClusterEndpoint = new ClusterEndpoint { Server = "https://b:6443" } },
            ]
        };

        var yaml = K3sReadinessHealthCheck.BuildConfigYaml(source, "https://new-server:6443");

        Assert.DoesNotContain("https://a:6443", yaml);
        Assert.DoesNotContain("https://b:6443", yaml);
        var occurrences = yaml.Split("https://new-server:6443").Length - 1;
        Assert.Equal(2, occurrences);
    }

    [Fact]
    public void BuildConfigYaml_WithNullClusters_DoesNotThrow()
    {
        var source = new K8SConfiguration { Clusters = null };
        var yaml = K3sReadinessHealthCheck.BuildConfigYaml(source, "https://localhost:6443");
        Assert.NotNull(yaml);
    }

    // ── IsTlsOrAuthFailure ────────────────────────────────────────────────────

    [Fact]
    public void IsTlsOrAuthFailure_WithAuthenticationException_ReturnsTrue()
    {
        var ex = new System.Security.Authentication.AuthenticationException("tls failed");
        Assert.True(K3sReadinessHealthCheck.IsTlsOrAuthFailure(ex));
    }

    [Fact]
    public void IsTlsOrAuthFailure_WithInnerAuthenticationException_ReturnsTrue()
    {
        var inner = new System.Security.Authentication.AuthenticationException("inner tls");
        var ex = new InvalidOperationException("outer", inner);
        Assert.True(K3sReadinessHealthCheck.IsTlsOrAuthFailure(ex));
    }

    [Fact]
    public void IsTlsOrAuthFailure_WithHttpOperationExceptionUnauthorized_ReturnsTrue()
    {
        var ex = new HttpOperationException
        {
            Response = new HttpResponseMessageWrapper(
                new HttpResponseMessage(HttpStatusCode.Unauthorized), "")
        };
        Assert.True(K3sReadinessHealthCheck.IsTlsOrAuthFailure(ex));
    }

    [Fact]
    public void IsTlsOrAuthFailure_WithHttpOperationExceptionForbidden_ReturnsFalse()
    {
        var ex = new HttpOperationException
        {
            Response = new HttpResponseMessageWrapper(
                new HttpResponseMessage(HttpStatusCode.Forbidden), "")
        };
        Assert.False(K3sReadinessHealthCheck.IsTlsOrAuthFailure(ex));
    }

    [Fact]
    public void IsTlsOrAuthFailure_WithRegularException_ReturnsFalse()
    {
        Assert.False(K3sReadinessHealthCheck.IsTlsOrAuthFailure(new InvalidOperationException("other")));
    }

    [Fact]
    public void IsTlsOrAuthFailure_WithHttpRequestException_ReturnsFalse()
    {
        Assert.False(K3sReadinessHealthCheck.IsTlsOrAuthFailure(new HttpRequestException("network")));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private const string MinimalKubeconfig = """
        apiVersion: v1
        clusters:
        - cluster:
            server: https://127.0.0.1:6443
            insecure-skip-tls-verify: true
          name: k8s
        contexts:
        - context:
            cluster: k8s
            user: default
          name: k8s
        current-context: k8s
        kind: Config
        preferences: {}
        users:
        - name: default
          user:
            token: test-token
        """;

    private static (K3sReadinessHealthCheck check, string dir, Mock<IKubernetes> mock) MakeCheck(
        bool writeKubeconfig,
        int nodeCount,
        int agentCount,
        Exception? listNodesThrows = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"k3s-hc-{Guid.NewGuid():N}");
        var clusterDir = Path.Combine(dir, "cluster");
        Directory.CreateDirectory(clusterDir);

        if (writeKubeconfig)
            File.WriteAllText(Path.Combine(clusterDir, "kubeconfig.yaml"), MinimalKubeconfig);

        var cluster = new K3sClusterResource("k8s") { KubeconfigDirectory = dir };
        for (var i = 0; i < agentCount; i++)
        {
            cluster.AgentCount++;
            cluster.AddAgentResource(new K3sAgentResource($"k8s-agent-{i}", cluster));
        }

        var mockCoreV1 = new Mock<ICoreV1Operations>();
        var mockK8s = new Mock<IKubernetes>();
        mockK8s.Setup(k => k.CoreV1).Returns(mockCoreV1.Object);

        // The source calls the extension method ListNodeAsync which calls
        // ListNodeWithHttpMessagesAsync on the interface.
        if (listNodesThrows is not null)
        {
            mockCoreV1
                .Setup(c => c.ListNodeWithHttpMessagesAsync(
                    It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<int?>(),
                    It.IsAny<bool?>(), It.IsAny<bool?>(),
                    It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(listNodesThrows);
        }
        else
        {
            var nodes = Enumerable.Range(0, nodeCount)
                .Select(_ => new V1Node
                {
                    Status = new V1NodeStatus
                    {
                        Conditions = [new V1NodeCondition { Type = "Ready", Status = "True" }]
                    }
                })
                .ToList();

            mockCoreV1
                .Setup(c => c.ListNodeWithHttpMessagesAsync(
                    It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<int?>(),
                    It.IsAny<bool?>(), It.IsAny<bool?>(),
                    It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpOperationResponse<V1NodeList>
                {
                    Body = new V1NodeList { Items = nodes },
                    Response = new HttpResponseMessage(),
                });
        }

        var check = new K3sReadinessHealthCheck(cluster, _ => mockK8s.Object);
        return (check, dir, mockK8s);
    }
}
