// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.ComponentModel;
using System.Diagnostics;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Manages Kubernetes manifest applies to a Kind cluster by orchestrating kubectl CLI calls.
/// </summary>
internal sealed class KubectlManager(
    IProcessRunner processRunner,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
    TimeSpan? clusterInfoMaxWait = null,
    TimeSpan? clusterInfoProbeTimeout = null)
{
    private const string KubectlNotFoundMessage = "kubectl CLI not found. Install it from https://kubernetes.io/docs/tasks/tools/";
    private static readonly TimeSpan ClusterInfoMaxWait = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ClusterInfoProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ClusterInfoInitialDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ClusterInfoMaxDelay = TimeSpan.FromSeconds(10);

    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync = delayAsync ?? Task.Delay;
    private readonly TimeSpan _clusterInfoMaxWait = clusterInfoMaxWait ?? ClusterInfoMaxWait;
    private readonly TimeSpan _clusterInfoProbeTimeout = clusterInfoProbeTimeout ?? ClusterInfoProbeTimeout;

    internal TimeSpan ClusterInfoMaxWaitForTesting => _clusterInfoMaxWait;

    /// <summary>
    /// Waits for the cluster API to answer, then applies the manifest via <c>kubectl apply</c>.
    /// </summary>
    public async Task ApplyAsync(K8sManifestResource resource, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var applyOptions = K8sManifestAnnotations.GetApplyOptions(resource);

        await WaitForClusterInfoAsync(resource, logger, cancellationToken).ConfigureAwait(false);
        ValidateRecursiveManifestTarget(resource);

        var args = CreateApplyArguments(resource);

        if (applyOptions.Recursive && applyOptions.IsKustomize)
        {
            logger.LogWarning(
                "Ignoring recursive apply for Kustomize manifest '{ManifestPath}' because kubectl apply -k does not support --recursive.",
                resource.ManifestPath);
        }
        else if (applyOptions.Recursive && resource.InlineContent is not null)
        {
            logger.LogWarning(
                "Ignoring recursive apply for inline manifest '{ManifestPath}' because kubectl apply -f - does not support --recursive.",
                resource.ManifestPath);
        }

        logger.LogInformation(
            "Applying manifest '{ManifestPath}' to cluster '{ClusterName}'...",
            resource.ManifestPath, resource.Parent.Name);

        ProcessResult result;
        var applyTimeout = KubectlTimeouts.Normalize(applyOptions.ApplyTimeout, nameof(resource.ApplyTimeout));
        using (var applyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            applyCts.CancelAfter(applyTimeout);

            try
            {
                result = await RunKubectlAsync(
                    logger,
                    args,
                    standardInput: resource.InlineContent,
                    cancellationToken: applyCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Timed out applying manifest '{resource.ManifestPath}' to cluster '{resource.Parent.Name}' after {applyTimeout}.");
            }
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to apply manifest '{resource.ManifestPath}' to cluster '{resource.Parent.Name}': {FormatFailureOutput(result)}");
        }

        var crdNames = GetAppliedCrdNames(result.Output);
        if (crdNames.Count > 0)
        {
            await WaitForCrdsAsync(crdNames, resource, logger, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Manifest '{ManifestPath}' applied successfully.", resource.ManifestPath);
    }

    /// <summary>
    /// Waits for applied CRDs to reach the Kubernetes <c>Established</c> condition.
    /// </summary>
    internal async Task WaitForCrdsAsync(
        IEnumerable<string> crdNames,
        K8sManifestResource resource,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(crdNames);
        ArgumentNullException.ThrowIfNull(resource);

        var crds = crdNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (crds.Length == 0)
        {
            return;
        }

        var waitPolicy = K8sManifestAnnotations.GetWaitPolicy(resource);
        var args = CreateWaitArguments(crds, resource.Parent.KubeconfigPath, waitPolicy.Crd.Timeout);

        logger.LogInformation(
            "Waiting for {CrdCount} custom resource definition(s) to become Established...",
            crds.Length);

        var result = await RunKubectlAsync(
            logger,
            args,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            if (waitPolicy.Crd.FailureBehavior == CrdWaitBehavior.BestEffort)
            {
                logger.LogWarning(
                    "Timed out or failed while waiting for custom resource definition(s) to become Established: {Error}",
                    message);
                return;
            }

            throw new InvalidOperationException(
                $"Timed out or failed while waiting for custom resource definition(s) to become Established: {message}");
        }
    }

    internal Task WaitForCrdsAsync(
        IEnumerable<string> crdNames,
        string kubeconfigPath,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken) =>
        WaitForCrdsCoreAsync(crdNames, kubeconfigPath, timeout, logger, cancellationToken, bestEffort: false);

    internal async Task<IReadOnlySet<string>> GetCustomResourceDefinitionsAsync(
        string kubeconfigPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kubeconfigPath);

        var result = await RunKubectlAsync(
            logger,
            CreateGetCrdsArguments(kubeconfigPath),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Failed to query custom resource definitions: " +
                (string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error));
        }

        return ParseResourceNames(result.Output);
    }

    /// <summary>
    /// Creates the <c>kubectl apply</c> argument list for a manifest resource.
    /// </summary>
    internal static IReadOnlyList<string> CreateApplyArguments(K8sManifestResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var applyOptions = K8sManifestAnnotations.GetApplyOptions(resource);
        var isKustomize = resource.InlineContent is null &&
            Directory.Exists(resource.ManifestPath) &&
            IsKustomizeDirectory(resource.ManifestPath);
        K8sManifestAnnotations.GetOrCreateApplyOptions(resource).IsKustomize = isKustomize;

        List<string> arguments =
        [
            "apply",
        ];

        if (resource.InlineContent is not null)
        {
            arguments.Add("-f");
            arguments.Add("-");
        }
        else if (isKustomize)
        {
            arguments.Add("-k");
            arguments.Add(resource.ManifestPath);
        }
        else
        {
            arguments.Add("-f");
            arguments.Add(resource.ManifestPath);
        }

        arguments.Add($"--kubeconfig={resource.Parent.KubeconfigPath}");

        if (!string.IsNullOrEmpty(resource.Namespace))
        {
            arguments.Add("--namespace");
            arguments.Add(resource.Namespace);
        }

        if (applyOptions.Recursive && !isKustomize && resource.InlineContent is null)
        {
            arguments.Add("--recursive");
        }

        if (applyOptions.ServerSide)
        {
            arguments.Add("--server-side");

            if (applyOptions.ForceConflicts)
            {
                arguments.Add("--force-conflicts");
            }
        }

        if (!string.IsNullOrEmpty(applyOptions.FieldManager))
        {
            arguments.Add("--field-manager");
            arguments.Add(applyOptions.FieldManager);
        }

        return arguments;
    }

    /// <summary>
    /// Creates the <c>kubectl wait</c> argument list for applied CRDs.
    /// </summary>
    internal static IReadOnlyList<string> CreateWaitArguments(
        IEnumerable<string> crdNames,
        string kubeconfigPath,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(crdNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(kubeconfigPath);

        List<string> arguments =
        [
            "wait",
            "--for=condition=Established",
        ];

        arguments.AddRange(crdNames);
        arguments.Add($"--timeout={KubectlTimeouts.ToSeconds(timeout, nameof(timeout))}s");
        arguments.Add($"--kubeconfig={kubeconfigPath}");

        return arguments;
    }

    /// <summary>
    /// Creates the <c>kubectl cluster-info</c> argument list for an API reachability probe.
    /// </summary>
    internal static IReadOnlyList<string> CreateClusterInfoArguments(string kubeconfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kubeconfigPath);

        return
        [
            "cluster-info",
            $"--kubeconfig={kubeconfigPath}",
        ];
    }

    internal static IReadOnlyList<string> CreateGetCrdsArguments(string kubeconfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kubeconfigPath);

        return
        [
            "get",
            "crd",
            "-o",
            "name",
            $"--kubeconfig={kubeconfigPath}",
        ];
    }

    /// <summary>
    /// Returns whether the directory contains a Kustomize marker file recognized by <c>kubectl apply -k</c>.
    /// </summary>
    internal static bool IsKustomizeDirectory(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (!Directory.Exists(directory))
        {
            return false;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Any(fileName =>
                string.Equals(fileName, "kustomization.yaml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "kustomization.yml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "kustomization", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Waits until the cluster API server is reachable through <c>kubectl cluster-info</c>.
    /// </summary>
    internal async Task WaitForClusterInfoAsync(
        K8sManifestResource resource,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var args = CreateClusterInfoArguments(resource.Parent.KubeconfigPath);
        string? lastFailureMessage = null;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_clusterInfoMaxWait);

        var pipeline = new ResiliencePipelineBuilder<ProcessResult>()
            .AddRetry(new RetryStrategyOptions<ProcessResult>
            {
                MaxRetryAttempts = int.MaxValue,
                Delay = TimeSpan.Zero,
                UseJitter = false,
                ShouldHandle = new PredicateBuilder<ProcessResult>()
                    .HandleResult(static result => result.ExitCode != 0),
                OnRetry = async arguments =>
                {
                    var delay = ComputeClusterInfoRetryDelay(arguments.AttemptNumber + 1);
                    var result = arguments.Outcome.Result!;
                    var error = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                    logger.LogWarning(
                        "Cluster '{ClusterName}' API is not reachable yet; retrying kubectl cluster-info in {DelaySeconds:n1}s. Last error: {Error}",
                        resource.Parent.Name,
                        delay.TotalSeconds,
                        error);
                    await _delayAsync(delay, arguments.Context.CancellationToken).ConfigureAwait(false);
                }
            })
            .Build();

        try
        {
            await pipeline.ExecuteAsync(async token =>
            {
                using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                probeCts.CancelAfter(_clusterInfoProbeTimeout);

                try
                {
                    var result = await RunKubectlAsync(
                        logger,
                        args,
                        cancellationToken: probeCts.Token).ConfigureAwait(false);
                    lastFailureMessage = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                    return result;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !timeoutCts.IsCancellationRequested)
                {
                    lastFailureMessage = "kubectl cluster-info probe timed out.";
                    return new ProcessResult(1, "", lastFailureMessage);
                }
            }, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Timed out waiting for cluster '{resource.Parent.Name}' API to become reachable" +
                (string.IsNullOrWhiteSpace(lastFailureMessage) ? "." : $": {lastFailureMessage}"));
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right)
    {
        return left <= right ? left : right;
    }

    private static TimeSpan ComputeClusterInfoRetryDelay(int failureCount)
    {
        var delay = TimeSpan.FromSeconds(ClusterInfoInitialDelay.TotalSeconds * Math.Pow(2, failureCount - 1));
        return Min(delay, ClusterInfoMaxDelay);
    }

    private async Task<ProcessResult> RunKubectlAsync(
        ILogger logger,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await processRunner.RunAsync(
                logger,
                "kubectl",
                arguments,
                standardInput: standardInput,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(KubectlNotFoundMessage, ex);
        }
    }

    private static string FormatFailureOutput(ProcessResult result)
    {
        var messages = new[]
        {
            result.Error?.Trim(),
            result.Output?.Trim(),
        };

        return string.Join(
            Environment.NewLine,
            messages.Where(static message => !string.IsNullOrWhiteSpace(message)).Distinct(StringComparer.Ordinal));
    }

    private static void ValidateRecursiveManifestTarget(K8sManifestResource resource)
    {
        var applyOptions = K8sManifestAnnotations.GetApplyOptions(resource);
        if (!applyOptions.Recursive || resource.InlineContent is not null || applyOptions.IsKustomize)
        {
            return;
        }

        if (!Directory.Exists(resource.ManifestPath))
        {
            throw new InvalidOperationException(
                $"Manifest '{resource.ManifestPath}' must be an existing directory when {nameof(KindManifestResourceBuilderExtensions.WithRecursive)} is used.");
        }
    }

    /// <summary>
    /// Extracts CRD resource names from <c>kubectl apply</c> output.
    /// </summary>
    private static IReadOnlyList<string> GetAppliedCrdNames(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var crds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!line.StartsWith("customresourcedefinition.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resourceName = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(resourceName))
            {
                crds.Add(resourceName);
            }
        }

        return [.. crds];
    }

    private async Task WaitForCrdsCoreAsync(
        IEnumerable<string> crdNames,
        string kubeconfigPath,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken,
        bool bestEffort)
    {
        ArgumentNullException.ThrowIfNull(crdNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(kubeconfigPath);

        var crds = crdNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (crds.Length == 0)
        {
            return;
        }

        var args = CreateWaitArguments(crds, kubeconfigPath, timeout);

        logger.LogInformation(
            "Waiting for {CrdCount} custom resource definition(s) to become Established...",
            crds.Length);

        var result = await RunKubectlAsync(
            logger,
            args,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        if (bestEffort)
        {
            logger.LogWarning(
                "Timed out or failed while waiting for custom resource definition(s) to become Established: {Error}",
                message);
            return;
        }

        throw new InvalidOperationException(
            $"Timed out or failed while waiting for custom resource definition(s) to become Established: {message}");
    }

    private static IReadOnlySet<string> ParseResourceNames(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}