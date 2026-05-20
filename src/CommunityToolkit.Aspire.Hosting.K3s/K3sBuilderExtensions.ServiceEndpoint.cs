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
    /// Exposes a Kubernetes service as a first-class Aspire endpoint resource.
    /// <para>
    /// An in-process KubernetesClient WebSocket port-forward is started when the cluster is ready,
    /// binding to <c>0.0.0.0:{hostPort}</c>. The endpoint only becomes healthy after the
    /// Kubernetes service has a ready pod — use <c>WaitForCompletion</c> on a
    /// <see cref="HelmReleaseResource"/> or <see cref="K8sManifestResource"/> to sequence the
    /// install before starting the port-forward.
    /// </para>
    /// </summary>
    [AspireExport("addServiceEndpoint",
        Description = "Exposes a Kubernetes service as an Aspire endpoint resource")]
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

        if (servicePort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(servicePort),
                servicePort, "Service port must be in the range 1–65535.");

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
    ///   <item>Host processes receive <c>http(s)://localhost:{port}</c>.</item>
    ///   <item>Container resources receive <c>http(s)://host.docker.internal:{port}</c>.
    ///     The <c>--add-host=host.docker.internal:host-gateway</c> runtime arg is injected
    ///     automatically so the hostname resolves on Linux Docker Engine.</item>
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
        var scheme = ep.Scheme;
        var envKey = $"services__{ep.Name}__url";

        if (destination.Resource is ContainerResource)
        {
            // Inject --add-host so host.docker.internal resolves inside Linux containers.
            // DCP does not inject this automatically; Docker Desktop on Mac/Windows resolves
            // it natively, but Docker Engine on Linux requires the explicit mapping.
            // ContainerRuntimeArgsCallbackAnnotation receives IList<object> directly.
            destination.Resource.Annotations.Add(
                new ContainerRuntimeArgsCallbackAnnotation(
                    args => args.Add("--add-host=host.docker.internal:host-gateway")));

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

            _ = Task.Run(() => forwarder.RunAsync(logger, ct), ct);
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
