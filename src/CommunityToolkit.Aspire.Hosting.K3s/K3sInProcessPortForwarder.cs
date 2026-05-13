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
                onReadyChanged(true);

                logger.LogInformation(
                    "Port-forward: 0.0.0.0:{Local} → svc/{Service}.{Ns}:{Port}",
                    localPort, serviceName, @namespace, servicePort);

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

            var selector = string.Join(",",
                (svc.Spec.Selector ?? new Dictionary<string, string>()).Select(kv => $"{kv.Key}={kv.Value}"));

            var pods = await k8sClient.CoreV1
                .ListNamespacedPodAsync(@namespace, labelSelector: selector, cancellationToken: ct)
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

            // Open WebSocket port-forward to the pod.
            using var ws = await k8sClient.WebSocketNamespacedPodPortForwardAsync(
                pod.Metadata.Name, @namespace, [servicePort],
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
