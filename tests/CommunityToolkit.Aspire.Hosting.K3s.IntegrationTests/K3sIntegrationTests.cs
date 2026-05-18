using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using k8s;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.K3s.IntegrationTests;

/// <summary>
/// End-to-end integration tests that spin up a real k3s cluster inside Docker.
/// <para>
/// Requirements:
/// <list type="bullet">
///   <item>A container runtime that supports privileged Linux containers.</item>
///   <item>Linux: Docker Engine 20.10+ or rootful Podman 4.0+.</item>
///   <item>macOS / Windows: Docker Desktop (containers run inside WSL2 / Hyper-V VM).</item>
/// </list>
/// No host-side <c>helm</c> or <c>kubectl</c> is needed — both run as Docker containers
/// (<c>alpine/helm</c> and <c>alpine/k8s</c>).
/// Tests are gated by <c>[RequiresDocker]</c> and run on both <c>ubuntu-latest</c>
/// and <c>windows-latest</c> CI jobs since privileged Linux containers work on Docker Desktop.
/// </para>
/// </summary>
[RequiresDocker]
public class K3sIntegrationTests : IAsyncLifetime
{
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;

    public async ValueTask InitializeAsync()
    {
        _builder = TestDistributedApplicationBuilder.Create();
        await ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        _builder?.Dispose();
    }

    [Fact]
    public async Task ClusterReachesRunningAndKubeconfigIsValid()
    {
        var cluster = _builder!.AddK3sCluster("k8s");
        _app = _builder.Build();

        await _app.StartAsync();

        var rns = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        await rns.WaitForResourceHealthyAsync("k8s", cts.Token);

        // local/kubeconfig.yaml must exist on the host.
        var kubeconfigPath = Path.Combine(
            _builder.AppHostDirectory, ".k3s", "k8s", "local", "kubeconfig.yaml");

        Assert.True(File.Exists(kubeconfigPath),
            $"Expected local kubeconfig at {kubeconfigPath}");

        // container/kubeconfig.yaml must also exist.
        var containerKubeconfigPath = Path.Combine(
            _builder.AppHostDirectory, ".k3s", "k8s", "container", "kubeconfig.yaml");
        Assert.True(File.Exists(containerKubeconfigPath),
            $"Expected container kubeconfig at {containerKubeconfigPath}");
    }

    [Fact]
    public async Task HelmReleaseExitsSuccessfully()
    {
        var cluster = _builder!.AddK3sCluster("k8s");

        cluster.AddHelmRelease(
            name: "nginx",
            chart: "nginx",
            repo: "https://charts.bitnami.com/bitnami",
            version: "18.3.6",
            @namespace: "nginx");

        _app = _builder.Build();
        await _app.StartAsync();

        var rns = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));

        await rns.WaitForResourceHealthyAsync("k8s", cts.Token);

        // HelmReleaseResource is a run-to-completion container — it exits with code 0
        // when helm upgrade --install --wait completes successfully.
        await rns.WaitForResourceAsync("nginx",
            s => s.Snapshot.State?.Text == "Exited", cts.Token);
    }

    [Fact]
    public async Task ServiceEndpointExposesHttpPort()
    {
        var cluster = _builder!.AddK3sCluster("k8s");

        var nginx = cluster.AddHelmRelease(
            name: "nginx",
            chart: "nginx",
            repo: "https://charts.bitnami.com/bitnami",
            version: "18.3.6",
            @namespace: "nginx");

        cluster.AddServiceEndpoint("nginx-web", "nginx", servicePort: 80, @namespace: "nginx")
            .WaitForCompletion(nginx);

        _app = _builder.Build();
        await _app.StartAsync();

        var rns = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));

        await rns.WaitForResourceHealthyAsync("k8s", cts.Token);
        await rns.WaitForResourceAsync("nginx",
            s => s.Snapshot.State?.Text == "Exited", cts.Token);
        await rns.WaitForResourceHealthyAsync("nginx-web", cts.Token);

        var model = _app.Services.GetRequiredService<DistributedApplicationModel>();
        var ep = model.Resources.OfType<K3sServiceEndpointResource>().Single();

        Assert.True(ep.HostPort > 0, "HostPort should be allocated");

        using var http = new HttpClient();
        var response = await http.GetAsync($"http://localhost:{ep.HostPort}", cts.Token);
        Assert.True(response.IsSuccessStatusCode,
            $"Expected HTTP 200 from nginx at localhost:{ep.HostPort}, got {response.StatusCode}");
    }

    [Fact]
    public async Task WithReferenceInjectsKubeconfigForProject()
    {
        var cluster = _builder!.AddK3sCluster("k8s");

        _app = _builder.Build();
        await _app.StartAsync();

        var rns = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        await rns.WaitForResourceHealthyAsync("k8s", cts.Token);

        var kubeconfigPath = Path.Combine(
            _builder.AppHostDirectory, ".k3s", "k8s", "local", "kubeconfig.yaml");

        var yaml = await File.ReadAllTextAsync(kubeconfigPath, cts.Token);
        Assert.Contains("localhost", yaml);
        Assert.DoesNotContain("127.0.0.1:6443", yaml);
    }

    [Fact]
    public async Task ManifestAppliesCrdAndReachesEstablished()
    {
        // Write a minimal CRD manifest to a temp file so AddK8sManifest can find it.
        var crdYaml = """
            apiVersion: apiextensions.k8s.io/v1
            kind: CustomResourceDefinition
            metadata:
              name: widgets.example.com
            spec:
              group: example.com
              scope: Namespaced
              names:
                plural: widgets
                singular: widget
                kind: Widget
              versions:
                - name: v1
                  served: true
                  storage: true
                  schema:
                    openAPIV3Schema:
                      type: object
                      properties:
                        spec:
                          type: object
                          properties:
                            color:
                              type: string
            """;

        var manifestDir = Path.Combine(_builder!.AppHostDirectory, "k8s-test-crds");
        Directory.CreateDirectory(manifestDir);
        var crdPath = Path.Combine(manifestDir, "widget-crd.yaml");
        await File.WriteAllTextAsync(crdPath, crdYaml);

        try
        {
            var cluster = _builder.AddK3sCluster("k8s");
            var crd = cluster.AddK8sManifest("widget-crd", crdPath);

            _app = _builder.Build();
            await _app.StartAsync();

            var rns = _app.Services.GetRequiredService<ResourceNotificationService>();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            await rns.WaitForResourceHealthyAsync("k8s", cts.Token);

            // K8sManifestResource is run-to-completion — exits 0 after CRD reaches Established.
            await rns.WaitForResourceAsync("widget-crd",
                s => s.Snapshot.State?.Text == "Exited", cts.Token);

            // Independently verify the CRD is Established via KubernetesClient.
            var kubeconfigPath = Path.Combine(
                _builder.AppHostDirectory, ".k3s", "k8s", "local", "kubeconfig.yaml");
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath);
            using var k8sClient = new Kubernetes(config);

            var widgetCrd = await k8sClient.ApiextensionsV1
                .ReadCustomResourceDefinitionAsync("widgets.example.com", cancellationToken: cts.Token);

            var established = widgetCrd.Status?.Conditions?.Any(c =>
                c.Type == "Established" &&
                string.Equals(c.Status, "True", StringComparison.OrdinalIgnoreCase)) == true;

            Assert.True(established, "CRD 'widgets.example.com' should be Established");
        }
        finally
        {
            Directory.Delete(manifestDir, recursive: true);
        }
    }

    [Fact]
    public async Task WithDataVolumePreservesStateAcrossRestarts()
    {
        // Use an explicit volume name shared between both app instances.
        var volumeName = $"aspire-k3s-persist-{Guid.NewGuid():N}";

        // ── First run ─────────────────────────────────────────────────────
        _builder!.AddK3sCluster("k8s").WithDataVolume(volumeName);
        _app = _builder.Build();
        await _app.StartAsync();

        var rns1 = _app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await rns1.WaitForResourceHealthyAsync("k8s", cts1.Token);

        var kubeconfigPath = Path.Combine(
            _builder.AppHostDirectory, ".k3s", "k8s", "local", "kubeconfig.yaml");

        using (var k8sClient = new Kubernetes(
            KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath)))
        {
            await k8sClient.CoreV1.CreateNamespacedConfigMapAsync(
                new V1ConfigMap
                {
                    Metadata = new V1ObjectMeta { Name = "persist-check" },
                    Data = new Dictionary<string, string> { ["run"] = "first" },
                },
                "default",
                cancellationToken: cts1.Token);
        }

        // Stop the first app. The volume is retained; the container is removed by DCP.
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;

        // ── Second run with the same named volume ─────────────────────────
        var builder2 = TestDistributedApplicationBuilder.Create();
        builder2.AddK3sCluster("k8s").WithDataVolume(volumeName);

        await using var app2 = builder2.Build();
        await app2.StartAsync();

        var rns2 = app2.Services.GetRequiredService<ResourceNotificationService>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await rns2.WaitForResourceHealthyAsync("k8s", cts2.Token);

        var kubeconfigPath2 = Path.Combine(
            builder2.AppHostDirectory, ".k3s", "k8s", "local", "kubeconfig.yaml");

        using var k8sClient2 = new Kubernetes(
            KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath2));

        var cm = await k8sClient2.CoreV1.ReadNamespacedConfigMapAsync(
            "persist-check", "default", cancellationToken: cts2.Token);

        Assert.Equal("first", cm.Data["run"]);

        await app2.StopAsync();
    }
}
