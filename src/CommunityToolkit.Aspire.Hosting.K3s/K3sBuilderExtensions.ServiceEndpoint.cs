using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for exposing Kubernetes services from a k3s cluster into the Aspire network.
/// </summary>
public static class K3sServiceEndpointExtensions
{
    /// <summary>
    /// Exposes a Kubernetes service as a first-class Aspire endpoint resource.
    /// <para>
    /// An in-process KubernetesClient WebSocket port-forward is started when the cluster is ready,
    /// binding to <c>0.0.0.0:{hostPort}</c>. Use <c>WaitFor</c> to sequence after a
    /// <see cref="HelmReleaseResource"/> or <see cref="K8sManifestResource"/> that deploys the service.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var nginx = cluster.AddHelmRelease("nginx", "nginx", repo: "https://charts.bitnami.com/bitnami");
    /// var ui = cluster.AddServiceEndpoint("nginx-ui", "nginx", servicePort: 80)
    ///     .WaitFor(nginx);
    /// builder.AddProject&lt;Projects.Api&gt;("api")
    ///     .WaitFor(ui)
    ///     .WithReference(ui);
    /// </code>
    /// </example>
    [AspireExport("addServiceEndpoint",
        Description = "Exposes a Kubernetes service as an Aspire endpoint resource")]
    public static IResourceBuilder<K3sServiceEndpointResource> AddServiceEndpoint(
        this IResourceBuilder<K3sClusterResource> builder,
        [ResourceName] string name,
        string serviceName,
        int servicePort,
        string @namespace = "default")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(serviceName);

        var cluster = builder.Resource;
        var endpoint = new K3sServiceEndpointResource(name, serviceName, servicePort, @namespace, cluster);

        var healthCheckKey = $"k3s_endpoint_{name}_ready";
        builder.ApplicationBuilder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckKey,
            sp => new K3sServiceEndpointHealthCheck(endpoint),
            failureStatus: HealthStatus.Unhealthy,
            tags: null));

        return builder.ApplicationBuilder
            .AddResource(endpoint)
            .ExcludeFromManifest()
            .WithHealthCheck(healthCheckKey)
            .WithIconName("ArrowRouting")
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "K3s Service Endpoint",
                State = KnownResourceStates.NotStarted,
                Properties =
                [
                    new ResourcePropertySnapshot("ServiceName", serviceName),
                    new ResourcePropertySnapshot("ServicePort", servicePort.ToString()),
                    new ResourcePropertySnapshot("Namespace", @namespace),
                ],
            });
    }

    /// <summary>
    /// Injects the service URL exposed by <paramref name="source"/> into
    /// <paramref name="destination"/> using the Aspire <c>services__{name}__url</c> convention.
    /// <list type="bullet">
    ///   <item>Host processes receive <c>https://localhost:{port}</c>.</item>
    ///   <item>Container resources receive <c>https://host.docker.internal:{port}</c>.
    ///     On Linux without Docker Desktop, add
    ///     <c>--add-host=host.docker.internal:host-gateway</c> to the container runtime args.</item>
    /// </list>
    /// </summary>
    [AspireExport("withReference",
        Description = "Injects the k3s service URL into a dependent resource")]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> destination,
        IResourceBuilder<K3sServiceEndpointResource> source)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);

        var ep = source.Resource;
        var scheme = ep.ServicePort is 443 or 8443 ? "https" : "http";
        var envKey = $"services__{ep.Name}__url";

        if (destination.Resource is ContainerResource)
        {
            return destination.WithEnvironment(ctx =>
            {
                if (ep.IsReady)
                    ctx.EnvironmentVariables[envKey] = $"{scheme}://host.docker.internal:{ep.HostPort}";
            });
        }

        return destination.WithEnvironment(ctx =>
        {
            if (ep.IsReady)
                ctx.EnvironmentVariables[envKey] = $"{scheme}://localhost:{ep.HostPort}";
        });
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    internal static async Task RunEndpointAsync(
        K3sServiceEndpointResource endpoint,
        K3sClusterResource cluster,
        ResourceNotificationService notifications,
        ILogger logger,
        CancellationToken ct)
    {
        await notifications.PublishUpdateAsync(endpoint,
            state => state with { State = KnownResourceStates.Starting })
            .ConfigureAwait(false);

        try
        {
            var kubeconfigPath = K3sBuilderExtensions.GetLocalKubeconfigPath(cluster);
            if (kubeconfigPath is null || !File.Exists(kubeconfigPath))
            {
                throw new InvalidOperationException(
                    "k3s local kubeconfig is not yet available for service endpoint.");
            }

            // Allocate a host port by opening a listener, reading the OS-assigned port,
            // then closing it before the forwarder binds — the port stays reserved in the
            // kernel TIME_WAIT for long enough that the forwarder wins the race.
            var hostPort = AllocatePort();
            endpoint.HostPort = hostPort;

            var scheme = endpoint.ServicePort is 443 or 8443 ? "https" : "http";

            var forwarder = new K3sInProcessPortForwarder(
                kubeconfigPath,
                endpoint.Namespace,
                endpoint.ServiceName,
                hostPort,
                endpoint.ServicePort,
                isReady =>
                {
                    endpoint.IsReady = isReady;
                    var state = isReady ? KnownResourceStates.Running : KnownResourceStates.RuntimeUnhealthy;
                    var urls = isReady
                        ? BuildUrls(scheme, endpoint.Name, hostPort, cluster.Name)
                        : ImmutableArray<UrlSnapshot>.Empty;

                    _ = notifications.PublishUpdateAsync(endpoint, s => s with
                    {
                        State = isReady ? KnownResourceStates.Running : KnownResourceStates.RuntimeUnhealthy,
                        Urls = urls,
                    });
                });

            _ = Task.Run(() => forwarder.RunAsync(logger, ct), ct);

            // Wait for the forwarder to signal ready (IsReady set via callback above).
            // The health check also reads IsReady, so WaitFor on dependent resources
            // naturally blocks until the port-forward is accepting connections.
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Service endpoint '{Name}' failed to start.", endpoint.Name);
            await notifications.PublishUpdateAsync(endpoint,
                state => state with { State = KnownResourceStates.FailedToStart })
                .ConfigureAwait(false);
        }
    }

    private static ImmutableArray<UrlSnapshot> BuildUrls(
        string scheme, string endpointName, int hostPort, string clusterName)
        => [
            new UrlSnapshot(endpointName, $"{scheme}://localhost:{hostPort}", IsInternal: false),
            new UrlSnapshot(
                $"{endpointName} (container)",
                $"{scheme}://host.docker.internal:{hostPort}",
                IsInternal: true),
        ];

    private static int AllocatePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

/// <summary>
/// Health check that satisfies <c>WaitFor(serviceEndpoint)</c>.
/// Returns <see cref="HealthCheckResult.Healthy"/> once the port-forward is active.
/// </summary>
internal sealed class K3sServiceEndpointHealthCheck(K3sServiceEndpointResource endpoint) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(endpoint.IsReady
            ? HealthCheckResult.Healthy("Port-forward is active")
            : HealthCheckResult.Unhealthy("Port-forward not yet active"));
}

#pragma warning restore ASPIREATS001
