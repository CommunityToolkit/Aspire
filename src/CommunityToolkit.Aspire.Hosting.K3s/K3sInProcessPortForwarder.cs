using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using k8s;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Forwards a local TCP port to a Kubernetes service using the KubernetesClient
/// WebSocket port-forward API — no <c>kubectl</c> binary required.
/// <para>
/// The listener binds to <c>0.0.0.0:{localPort}</c> so both host processes
/// (<c>localhost:{port}</c>) and DCP-network containers
/// (<c>host.docker.internal:{port}</c>) can reach the service.
/// </para>
/// <para>
/// The <paramref name="onReadyChanged"/> callback is invoked with <see langword="true"/>
/// only after a ready pod is confirmed via <c>ListNamespacedPodAsync</c> — not when the
/// TCP listener starts. This ensures <c>WaitFor(endpoint)</c> on dependent resources
/// correctly waits until the k8s service has a reachable pod.
/// </para>
/// </summary>
internal sealed class K3sInProcessPortForwarder(
    string kubeconfigPath,
    string @namespace,
    string serviceName,
    int localPort,
    int servicePort,
    Action<bool> onReadyChanged)
{
    public async Task RunAsync(ILogger logger, CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(2);

        while (!ct.IsCancellationRequested)
        {
            var listener = new TcpListener(IPAddress.Any, localPort);
            try
            {
                listener.Start();

                logger.LogInformation(
                    "Port-forward: 0.0.0.0:{Local} → svc/{Service}.{Ns}:{Port}",
                    localPort, serviceName, @namespace, servicePort);

                // Probe the service before signalling ready — the Kubernetes service and
                // a ready pod must exist before any connection can succeed.
                // This makes the ready signal meaningful for WaitFor(endpoint) consumers.
                await WaitForServiceReadyAsync(logger, ct).ConfigureAwait(false);
                onReadyChanged(true);

                while (!ct.IsCancellationRequested)
                {
                    var tcp = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                    _ = Task.Run(
                        () => ForwardConnectionAsync(tcp, logger, ct),
                        CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Port-forward for svc/{Service} failed; retrying in {Delay}s…",
                    serviceName, backoff.TotalSeconds);
                onReadyChanged(false);
            }
            finally
            {
                listener.Stop();
            }

            if (ct.IsCancellationRequested) break;

            try { await Task.Delay(backoff, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }
    }

    /// <summary>
    /// Polls until the named service has at least one fully-ready pod.
    /// This ensures the ready signal is only emitted when connections can actually succeed.
    /// </summary>
    private async Task WaitForServiceReadyAsync(ILogger logger, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath);
                using var k8sClient = new Kubernetes(config);

                var svc = await k8sClient.CoreV1
                    .ReadNamespacedServiceAsync(serviceName, @namespace, cancellationToken: ct)
                    .ConfigureAwait(false);

                var exposesRequestedPort = svc.Spec?.Ports?.Any(p => p.Port == servicePort) == true;
                if (!exposesRequestedPort)
                {
                    logger.LogDebug(
                        "Service {Service}/{Ns} does not expose requested port {ServicePort}; retrying…",
                        serviceName, @namespace, servicePort);
                }
                else if (svc.Spec?.Selector is null or { Count: 0 })
                {
                    logger.LogWarning(
                        "Service {Service}/{Ns} has no pod selector — cannot determine readiness.",
                        serviceName, @namespace);
                    return;
                }
                else
                {
                    var labelSelector = string.Join(",",
                        svc.Spec.Selector.Select(kv => $"{kv.Key}={kv.Value}"));

                    var pods = await k8sClient.CoreV1
                        .ListNamespacedPodAsync(@namespace, labelSelector: labelSelector, cancellationToken: ct)
                        .ConfigureAwait(false);

                    var hasReadyPod = pods.Items.Any(p =>
                        p.Status?.Phase == "Running" &&
                        p.Status?.ContainerStatuses?.All(c => c.Ready) == true);

                    if (hasReadyPod)
                    {
                        logger.LogDebug(
                            "Service {Service}/{Ns} exposes requested port {ServicePort} and has a ready pod — port-forward is ready.",
                            serviceName, @namespace, servicePort);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex,
                    "Service {Service}/{Ns} not yet ready; retrying…", serviceName, @namespace);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
        }
    }

    private async Task ForwardConnectionAsync(TcpClient tcp, ILogger logger, CancellationToken ct)
    {
        using var _ = tcp;
        try
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath);
            using var k8sClient = new Kubernetes(config);

            // Resolve the service to a running pod.
            var svc = await k8sClient.CoreV1
                .ReadNamespacedServiceAsync(serviceName, @namespace, cancellationToken: ct)
                .ConfigureAwait(false);

            if (svc.Spec.Selector is null or { Count: 0 })
            {
                logger.LogWarning(
                    "Service {Service}/{Ns} has no pod selector — connection dropped.",
                    serviceName, @namespace);
                return;
            }

            var labelSelector = string.Join(",",
                svc.Spec.Selector.Select(kv => $"{kv.Key}={kv.Value}"));

            var pods = await k8sClient.CoreV1
                .ListNamespacedPodAsync(@namespace, labelSelector: labelSelector, cancellationToken: ct)
                .ConfigureAwait(false);

            var pod = pods.Items.FirstOrDefault(p =>
                p.Status?.Phase == "Running" &&
                p.Status?.ContainerStatuses?.All(c => c.Ready) == true);

            if (pod is null)
            {
                logger.LogWarning(
                    "No ready pod found for service {Service}/{Ns} — connection dropped.",
                    serviceName, @namespace);
                return;
            }

            // Resolve the pod container port from the service's targetPort.
            // WebSocketNamespacedPodPortForwardAsync requires the container port, not the
            // service port. targetPort can be a numeric string or a named port string.
            var svcPort = svc.Spec.Ports.FirstOrDefault(p => p.Port == servicePort);
            int podPort;
            if (svcPort?.TargetPort?.Value is { } tp)
            {
                if (int.TryParse(tp, out var numeric))
                {
                    podPort = numeric;
                }
                else
                {
                    // Named targetPort — resolve against the selected pod's container ports.
                    podPort = pod.Spec.Containers
                        .SelectMany(c => c.Ports ?? [])
                        .FirstOrDefault(p => p.Name == tp)
                        ?.ContainerPort ?? servicePort;
                }
            }
            else
            {
                podPort = servicePort;
            }

            // Open WebSocket port-forward to the pod.
            using var ws = await k8sClient.WebSocketNamespacedPodPortForwardAsync(
                pod.Metadata.Name, @namespace, [podPort],
                cancellationToken: ct).ConfigureAwait(false);

            using var demuxer = new StreamDemuxer(ws, StreamType.PortForward);
            demuxer.Start();

            using var k8sStream = demuxer.GetStream((byte?)0, (byte?)0);
            using var tcpStream = tcp.GetStream();

            // Bidirectional byte pump until either side closes.
            await Task.WhenAny(
                tcpStream.CopyToAsync(k8sStream, ct),
                k8sStream.CopyToAsync(tcpStream, ct)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Port-forward connection for svc/{Service} closed.", serviceName);
        }
    }
}
