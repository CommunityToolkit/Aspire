using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting;
using k8s;
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
    /// Exposes a Kubernetes Service from the cluster as an Aspire endpoint resource.
    /// </summary>
    /// <param name="builder">The k3s cluster resource builder.</param>
    /// <param name="name">The Aspire resource name for this endpoint.</param>
    /// <param name="serviceName">
    /// The name of the Kubernetes Service to forward, as it appears in <c>kubectl get svc</c>.
    /// </param>
    /// <param name="servicePort">
    /// The port number declared on the Kubernetes Service (the <c>port</c> field, not
    /// <c>targetPort</c>). Must be in the range 1–65535.
    /// </param>
    /// <param name="namespace">
    /// The Kubernetes namespace that contains the Service. Defaults to <c>default</c>.
    /// </param>
    /// <param name="scheme">
    /// The URL scheme (<c>http</c> or <c>https</c>) for the injected environment variable.
    /// When <see langword="null"/> (the default), the scheme is inferred from the port:
    /// ports 443 and 8443 use <c>https</c>, all others use <c>http</c>.
    /// </param>
    /// <returns>A builder for the <see cref="K3sServiceEndpointResource"/>.</returns>
    /// <remarks>
    /// <para>
    /// An in-process WebSocket port-forward is started when the cluster becomes ready,
    /// binding to <c>0.0.0.0:{allocatedHostPort}</c> so both host processes and DCP-network
    /// containers can reach the service.
    /// </para>
    /// <para>
    /// The endpoint transitions to <c>Running</c> only after the target Kubernetes Service
    /// has at least one ready pod. Sequence chart installs or manifest applies before this
    /// resource using <c>WaitForCompletion</c> on <see cref="HelmReleaseResource"/> or
    /// <see cref="K8sManifestResource"/> to prevent the port-forward from polling indefinitely.
    /// </para>
    /// <para>
    /// Use <c>WithReference(endpoint)</c> on a dependent resource builder to inject the
    /// service URL as <c>services__{name}__url</c>. Host processes receive
    /// <c>http(s)://localhost:{port}</c>; containers receive
    /// <c>http(s)://host.docker.internal:{port}</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="name"/>, or <paramref name="serviceName"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="serviceName"/> or <paramref name="namespace"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="servicePort"/> is not in the range 1–65535.
    /// </exception>
    [AspireExport]
    public static IResourceBuilder<K3sServiceEndpointResource> AddServiceEndpoint(
        this IResourceBuilder<K3sClusterResource> builder,
        [ResourceName] string name,
        string serviceName,
        int servicePort,
        string @namespace = "default",
        string? scheme = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);

        // Infer scheme from the port when not explicitly provided.
        // Callers should pass an explicit scheme whenever the Kubernetes service port
        // does not reliably indicate the application protocol (e.g. HTTPS on port 80).
        var resolvedScheme = scheme ?? (servicePort is 443 or 8443 ? "https" : "http");

        var cluster = builder.Resource;
        var endpoint = new K3sServiceEndpointResource(name, serviceName, servicePort, @namespace, cluster)
        {
            Scheme = resolvedScheme,
        };

        var healthCheckKey = $"k3s_endpoint_{name}_ready";
        builder.ApplicationBuilder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckKey,
            sp => new K3sServiceEndpointHealthCheck(endpoint),
            failureStatus: HealthStatus.Unhealthy,
            tags: null));

        // Dispose the forwarder when the parent cluster stops so the port is released
        // promptly rather than waiting for the application-lifetime CT to fire.
        // ResourceStoppedEvent fires after the cluster container has stopped — at that
        // point port-forwarding is no longer useful regardless.
        builder.ApplicationBuilder.Eventing.Subscribe<ResourceStoppedEvent>(
            cluster,
            async (@event, ct) =>
            {
                if (endpoint.Forwarder is { } forwarder)
                    await forwarder.DisposeAsync().ConfigureAwait(false);
            });

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

    // WithReference(K3sServiceEndpointResource) is no longer a custom extension.
    // K3sServiceEndpointResource implements IResourceWithConnectionString, so the standard
    // Aspire WithReference(IResourceBuilder<IResourceWithConnectionString>) overload injects
    // services__{name}__url=http://localhost:{port} for host processes automatically.
    // The BeforeStartEvent subscriber in AddK3sCluster overrides the URL to
    // http://host.docker.internal:{port} for containers and adds --add-host.

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

            var hostPort = AllocatePort();
            endpoint.HostPort = hostPort;

            var scheme = endpoint.Scheme;

            var forwarder = new K3sInProcessPortForwarder(
                kubeconfigPath,
                endpoint.Namespace,
                endpoint.ServiceName,
                hostPort,
                endpoint.ServicePort,
                isReady =>
                {
                    endpoint.IsReady = isReady;
                    var urls = isReady
                        ? BuildUrls(scheme, endpoint.Name, hostPort, cluster.Name)
                        : ImmutableArray<UrlSnapshot>.Empty;

                    _ = notifications.PublishUpdateAsync(endpoint, s => s with
                    {
                        State = isReady ? KnownResourceStates.Running : KnownResourceStates.RuntimeUnhealthy,
                        Urls = urls,
                    });
                });

            // Retain the forwarder so ResourceStoppedEvent on the cluster can dispose it
            // independently of the application-lifetime cancellation token.
            endpoint.Forwarder = forwarder;

            _ = Task.Run(async () =>
            {
                await using (forwarder.ConfigureAwait(false))
                {
                    await forwarder.RunAsync(logger, ct).ConfigureAwait(false);
                }
            }, ct);
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
        // Probe on IPAddress.Any to match the forwarder's actual bind address.
        // Probing on Loopback while the forwarder binds on Any can miss conflicts
        // on non-loopback interfaces, producing a SocketException at bind time.
        using var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

/// <summary>
/// Health check that satisfies <c>WaitFor(serviceEndpoint)</c>.
/// Returns <see cref="HealthCheckResult.Healthy"/> once the port-forward has a confirmed
/// connection to a ready pod.
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
