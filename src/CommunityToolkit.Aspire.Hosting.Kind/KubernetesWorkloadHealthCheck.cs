// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Health check that evaluates the readiness of Kubernetes workloads.
/// </summary>
internal sealed class KubernetesWorkloadHealthCheck : IHealthCheck, IDisposable
{
    private readonly IKubernetes _client;
    private readonly string _labelSelector;
    private readonly string? _namespace;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new workload health check.
    /// </summary>
    /// <param name="kubeconfigPath">Path to the kubeconfig file for the target cluster.</param>
    /// <param name="labelSelector">Kubernetes label selector to match workloads.</param>
    /// <param name="namespace">Namespace to query, or <see langword="null"/> for all namespaces.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public KubernetesWorkloadHealthCheck(
        string kubeconfigPath,
        string labelSelector,
        string? @namespace,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(kubeconfigPath);
        _labelSelector = labelSelector ?? throw new ArgumentNullException(nameof(labelSelector));
        _namespace = @namespace;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        KubernetesClientConfiguration config =
            KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath);
        _client = new Kubernetes(config);
    }

    private string Description => _namespace is not null
        ? $"{_labelSelector} in {_namespace}"
        : _labelSelector;

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            KubernetesWorkloadStatus workloadStatus = await KubernetesWorkloadStatusClient
                .GetWorkloadStatusAsync(_client, _labelSelector, _namespace, _logger, cancellationToken)
                .ConfigureAwait(false);

            if (workloadStatus.Workloads.Count == 0)
            {
                return HealthCheckResult.Healthy($"'{Description}' is deployed (no tracked workloads found).");
            }

            if (workloadStatus.Workloads.All(status => status.IsReady()))
            {
                return HealthCheckResult.Healthy($"'{Description}' is ready.");
            }

            return HealthCheckResult.Unhealthy($"'{Description}' is not ready.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error checking workload status for '{Description}'", Description);
            return HealthCheckResult.Unhealthy($"Unable to query workloads for '{Description}'.");
        }
    }
}
