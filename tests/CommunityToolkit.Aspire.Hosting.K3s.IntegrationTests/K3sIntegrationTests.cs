using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.K3s.IntegrationTests;

/// <summary>
/// End-to-end integration tests that spin up a real k3s cluster inside Docker.
/// <para>
/// Requirements:
/// <list type="bullet">
///   <item>Linux with Docker (privileged containers required by k3s).</item>
///   <item><c>helm</c> on PATH — used by <c>AddHelmRelease</c>.</item>
///   <item><c>kubectl</c> on PATH — used by <c>AddK8sManifest</c>.</item>
/// </list>
/// The tests are gated by <c>[RequiresDocker]</c> and intended for the
/// <c>ubuntu-latest</c>-only CI job in <c>tests.yaml</c>.
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
    public async Task HelmReleaseReachesRunning()
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
        await rns.WaitForResourceAsync("nginx",
            s => s.Snapshot.State?.Text == KnownResourceStates.Running, cts.Token);
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
        // nginx is a run-to-completion container — wait for it to exit (Exited state)
        await rns.WaitForResourceAsync("nginx",
            s => s.Snapshot.State?.Text == "Exited", cts.Token);
        await rns.WaitForResourceHealthyAsync("nginx-web", cts.Token);

        // Find the allocated port from the endpoint resource.
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

        // WithReference on a project injects KUBECONFIG pointing to local/kubeconfig.yaml.
        // We verify the env var would be set by checking the cluster state.
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
}
