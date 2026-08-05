// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Manages Helm chart deployments to a Kind cluster by orchestrating Helm CLI calls.
/// </summary>
internal sealed class HelmManager(
    IProcessRunner processRunner,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
    KubectlManager? kubectlManager = null)
{
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
        var attempt = 0;
        var pipeline = new ResiliencePipelineBuilder<HelmInstallAttemptResult>()
            .AddRetry(new RetryStrategyOptions<HelmInstallAttemptResult>
            {
                MaxRetryAttempts = Math.Max(0, maxAttempts - 1),
                Delay = TimeSpan.Zero,
                UseJitter = false,
                ShouldHandle = new PredicateBuilder<HelmInstallAttemptResult>()
                    .HandleResult(static result => result.ShouldRetry),
                OnRetry = async arguments =>
                {
                    var retryResult = arguments.Outcome.Result!;
                    logger.LogWarning(
                        "Helm release '{ReleaseName}' failed before CRDs finished registering. Waiting for {CrdCount} CRD(s) before retrying.",
                        resource.ReleaseName,
                        retryResult.NewCrds.Length);

                    await _kubectlManager.WaitForCrdsAsync(
                        retryResult.NewCrds,
                        resource.Parent.KubeconfigPath,
                        resource.CrdWaitRetryTimeout,
                        logger,
                        arguments.Context.CancellationToken).ConfigureAwait(false);

                    knownCrds = retryResult.DiscoveredCrds;
                    var backoff = ComputeRetryBackoff(resource.CrdWaitRetryBackoff, arguments.AttemptNumber + 1);
                    logger.LogInformation(
                        "Retrying Helm release '{ReleaseName}' in {DelaySeconds:n1}s.",
                        resource.ReleaseName,
                        backoff.TotalSeconds);
                    await _delayAsync(backoff, arguments.Context.CancellationToken).ConfigureAwait(false);
                }
            })
            .Build();

        var finalResult = await pipeline.ExecuteAsync(async token =>
        {
            attempt++;
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
                cancellationToken: token).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                return HelmInstallAttemptResult.Success(result);
            }

            if (attempt >= maxAttempts || !ShouldRetryForCrdRace(result))
            {
                return HelmInstallAttemptResult.Fail(result);
            }

            var discoveredCrds = await TryGetCustomResourceDefinitionsAsync(resource, logger, token).ConfigureAwait(false);
            var newCrds = discoveredCrds
                .Except(knownCrds, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return newCrds.Length == 0
                ? HelmInstallAttemptResult.Fail(result)
                : HelmInstallAttemptResult.Retry(result, discoveredCrds, newCrds);
        }, cancellationToken).ConfigureAwait(false);

        if (finalResult.Result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to install Helm chart '{resource.ChartRef}' as release '{resource.ReleaseName}': {FormatFailureOutput(finalResult.Result)}");
        }

        logger.LogInformation(
            "Helm release '{ReleaseName}' installed successfully.", resource.ReleaseName);
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

    private static string FormatFailureOutput(ProcessResult result)
    {
        return string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
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

    private sealed record HelmInstallAttemptResult(ProcessResult Result, bool ShouldRetry, IReadOnlySet<string> DiscoveredCrds, string[] NewCrds)
    {
        public static HelmInstallAttemptResult Success(ProcessResult result) =>
            new(result, ShouldRetry: false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);

        public static HelmInstallAttemptResult Fail(ProcessResult result) =>
            new(result, ShouldRetry: false, new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);

        public static HelmInstallAttemptResult Retry(ProcessResult result, IReadOnlySet<string> discoveredCrds, string[] newCrds) =>
            new(result, ShouldRetry: true, discoveredCrds, newCrds);
    }
}
