using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Forwards a local TCP port to a Kubernetes service by running
/// <c>kubectl port-forward service/{name} {localPort}:{servicePort} -n {namespace}</c>
/// as a managed subprocess.
/// <para>
/// Mirrors what a developer types in a terminal — the most reliable approach for
/// k3s-in-Docker because kubectl handles WebSocket negotiation, kubelet routing,
/// and reconnect logic internally.
/// </para>
/// </summary>
internal sealed class K3sInProcessPortForwarder(
    string kubeconfigYaml,
    string @namespace,
    string serviceName,
    int localPort,
    int servicePort)
{
    public async Task RunAsync(ILogger logger, CancellationToken ct)
    {
        logger.LogInformation(
            "Port-forward: localhost:{Local} → svc/{Service}.{Ns}:{Port}",
            localPort, serviceName, @namespace, servicePort);

        var tempConfig = Path.Combine(
            Path.GetTempPath(),
            $"aspire-k3s-pf-{Environment.ProcessId}-{serviceName}.yaml");

        await File.WriteAllTextAsync(tempConfig, kubeconfigYaml, Encoding.UTF8, ct)
            .ConfigureAwait(false);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await RunKubectlAsync(tempConfig, logger, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Port-forward for svc/{Service} exited; restarting in 5 s…", serviceName);

                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            try { File.Delete(tempConfig); } catch { /* best-effort cleanup */ }
        }
    }

    private async Task RunKubectlAsync(string kubeconfigPath, ILogger logger, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("kubectl")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("port-forward");
        psi.ArgumentList.Add($"service/{serviceName}");
        psi.ArgumentList.Add($"{localPort}:{servicePort}");
        psi.ArgumentList.Add("-n");
        psi.ArgumentList.Add(@namespace);
        psi.ArgumentList.Add($"--kubeconfig={kubeconfigPath}");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start kubectl port-forward.");

        using var reg = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
        });

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) logger.LogDebug("{Line}", e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) logger.LogDebug("{Line}", e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0 && !ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"kubectl port-forward exited with code {process.ExitCode}.");
        }
    }
}
