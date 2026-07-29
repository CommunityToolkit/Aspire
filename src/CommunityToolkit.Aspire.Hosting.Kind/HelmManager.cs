// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Manages Helm chart deployments to a Kind cluster by orchestrating Helm CLI calls.
/// </summary>
internal sealed class HelmManager(
    IProcessRunner processRunner,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
    KubectlManager? kubectlManager = null)
{
    private static readonly TimeSpan DefaultCrdWaitTimeout = TimeSpan.FromMinutes(5);

    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync = delayAsync ?? Task.Delay;
    private readonly KubectlManager _kubectlManager = kubectlManager ?? new KubectlManager(processRunner, delayAsync);

    /// <summary>
    /// Installs or upgrades the Helm release.
    /// </summary>
    public async Task InstallAsync(KindHelmChartResource resource, ILogger logger, CancellationToken cancellationToken)
    {
        var args = CreateInstallArguments(resource);
        var maxAttempts = resource.CrdWaitRetryMaxAttempts;
        IReadOnlySet<string> knownCrds = maxAttempts > 1
            ? await TryGetCustomResourceDefinitionsAsync(resource, logger, cancellationToken).ConfigureAwait(false)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            logger.LogInformation(
                "Installing Helm chart '{ChartRef}' as release '{ReleaseName}' in cluster '{ClusterName}' (attempt {Attempt}/{MaxAttempts})...",
                resource.ChartRef,
                resource.ReleaseName,
                resource.Parent.Name,
                attempt,
                maxAttempts);

            var result = await processRunner.RunAsync(
                logger,
                "helm",
                args,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                logger.LogInformation(
                    "Helm release '{ReleaseName}' installed successfully.", resource.ReleaseName);
                return;
            }

            if (attempt >= maxAttempts || !ShouldRetryForCrdRace(result))
            {
                throw new InvalidOperationException(
                    $"Failed to install Helm chart '{resource.ChartRef}' as release '{resource.ReleaseName}': {result.Error}");
            }

            var discoveredCrds = await TryGetCustomResourceDefinitionsAsync(resource, logger, cancellationToken).ConfigureAwait(false);
            var newCrds = discoveredCrds
                .Except(knownCrds, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (newCrds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to install Helm chart '{resource.ChartRef}' as release '{resource.ReleaseName}': {result.Error}");
            }

            logger.LogWarning(
                "Helm release '{ReleaseName}' failed before CRDs finished registering. Waiting for {CrdCount} CRD(s) before retrying.",
                resource.ReleaseName,
                newCrds.Length);

            await _kubectlManager.WaitForCrdsAsync(
                newCrds,
                resource.Parent.KubeconfigPath,
                DefaultCrdWaitTimeout,
                logger,
                cancellationToken).ConfigureAwait(false);

            knownCrds = discoveredCrds;
            var backoff = ComputeRetryBackoff(resource.CrdWaitRetryBackoff, attempt);
            logger.LogInformation(
                "Retrying Helm release '{ReleaseName}' in {DelaySeconds:n1}s.",
                resource.ReleaseName,
                backoff.TotalSeconds);
            await _delayAsync(backoff, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static IReadOnlyList<string> CreateInstallArguments(KindHelmChartResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        List<string> arguments =
        [
            "upgrade",
            "--install",
            resource.ReleaseName,
            resource.ChartRef,
            $"--kubeconfig={resource.Parent.KubeconfigPath}",
        ];

        if (!string.IsNullOrEmpty(resource.Version))
        {
            arguments.Add("--version");
            arguments.Add(resource.Version);
        }

        if (!string.IsNullOrEmpty(resource.Namespace))
        {
            arguments.Add("--namespace");
            arguments.Add(resource.Namespace);
            arguments.Add("--create-namespace");
        }

        foreach (var (key, value) in resource.Values)
        {
            arguments.Add("--set");
            arguments.Add($"{key}={value}");
        }

        foreach (var (key, value) in resource.StringValues)
        {
            arguments.Add("--set-string");
            arguments.Add($"{key}={value}");
        }

        foreach (string valuesFile in resource.ValuesFiles)
        {
            arguments.Add("-f");
            arguments.Add(valuesFile);
        }

        return arguments;
    }

    internal static TimeSpan ComputeRetryBackoff(TimeSpan initialBackoff, int failureCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureCount, 1);

        if (failureCount >= 64)
        {
            return TimeSpan.MaxValue;
        }

        var multiplier = 1L << (failureCount - 1);
        return initialBackoff.Ticks > TimeSpan.MaxValue.Ticks / multiplier
            ? TimeSpan.MaxValue
            : TimeSpan.FromTicks(initialBackoff.Ticks * multiplier);
    }

    private static bool ShouldRetryForCrdRace(ProcessResult result)
    {
        var combined = string.Concat(result.Error, "\n", result.Output);
        return combined.Contains("no matches for kind", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("ensure CRDs are installed first", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlySet<string>> TryGetCustomResourceDefinitionsAsync(
        KindHelmChartResource resource,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _kubectlManager.GetCustomResourceDefinitionsAsync(
                resource.Parent.KubeconfigPath,
                logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Unable to snapshot CRDs for Helm release '{ReleaseName}'.",
                resource.ReleaseName);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
