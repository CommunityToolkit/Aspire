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
    Action<bool> onReadyChanged,
    Func<string, IKubernetes>? kubernetesFactory = null) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;

    // Creates a client from the kubeconfig path using the injected factory if provided,
    // or the default KubernetesClient.BuildConfigFromConfigFile path. The factory
    // parameter exists to enable mock injection in unit tests without a real cluster.
    private IKubernetes CreateClient() =>
        kubernetesFactory is not null
            ? kubernetesFactory(kubeconfigPath)
            : new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath));

    public async Task RunAsync(ILogger logger, CancellationToken ct)
    {
        // Link the outer application-lifetime token with our own so either can stop the loop.
        // _cts.Token lets DisposeAsync cancel independently of the outer token (e.g. when
        // the cluster resource stops before the full AppHost shuts down).
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var linkedCt = linked.Token;

        var backoff = TimeSpan.FromSeconds(2);
        var currentPort = localPort;

        while (!linkedCt.IsCancellationRequested)
        {
            var listener = new TcpListener(IPAddress.Any, currentPort);
            _listener = listener;
            try
            {
                listener.Start();

                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation(
                        "Port-forward: 0.0.0.0:{Local} → svc/{Service}.{Ns}:{Port}",
                        currentPort, serviceName, @namespace, servicePort);

                // Probe the service before signalling ready — the Kubernetes service and
                // a ready pod must exist before any connection can succeed.
                // This makes the ready signal meaningful for WaitFor(endpoint) consumers.
                await WaitForServiceReadyAsync(logger, linkedCt).ConfigureAwait(false);
                onReadyChanged(true);

                while (!linkedCt.IsCancellationRequested)
                {
                    var tcp = await listener.AcceptTcpClientAsync(linkedCt).ConfigureAwait(false);
                    _ = Task.Run(
                        () => ForwardConnectionAsync(tcp, logger, linkedCt),
                        linkedCt);
                }
            }
            catch (OperationCanceledException) when (linkedCt.IsCancellationRequested)
            {
                break;
            }
            catch (InvalidOperationException ioe) when (!linkedCt.IsCancellationRequested)
            {
                // Non-retryable configuration error (e.g. service has no pod selector).
                // Log and stop — retrying will never succeed.
                logger.LogError(ioe, "Port-forward for svc/{Service}/{Ns} cannot be established.",
                    serviceName, @namespace);
                onReadyChanged(false);
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Port-forward for svc/{Service} failed; retrying in {Delay}s…",
                    serviceName, backoff.TotalSeconds);
                onReadyChanged(false);
                // Allocate a fresh port on retry — the previous one may have been
                // stolen between our probe and the forwarder bind (TOCTOU).
                using var probe = new TcpListener(IPAddress.Any, 0);
                probe.Start();
                currentPort = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
            }
            finally
            {
                _listener = null;
                listener.Stop();
            }

            if (linkedCt.IsCancellationRequested) break;

            try { await Task.Delay(backoff, linkedCt).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }

        onReadyChanged(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Cancel the linked CTS so RunAsync exits its loop even if the outer
        // application-lifetime token is still live (e.g. cluster stopped but AppHost
        // is still running).
        await _cts.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();
        _cts.Dispose();
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
                using var k8sClient = CreateClient();

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
                    throw new InvalidOperationException(
                        $"Service {serviceName}/{@namespace} has no pod selector and cannot be port-forwarded by {nameof(K3sInProcessPortForwarder)}.");
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
                            "Service {Service}/{Ns} exposes port {Port} and has a ready pod — port-forward is ready.",
                            serviceName, @namespace, servicePort);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                throw; // Non-retryable — let RunAsync catch it.
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
            using var k8sClient = CreateClient();

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
