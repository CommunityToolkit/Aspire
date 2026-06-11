// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Manages Helm chart deployments to a Kind cluster by orchestrating Helm CLI calls.
/// </summary>
internal sealed class HelmManager(IProcessRunner processRunner)
{
    /// <summary>
    /// Installs or upgrades the Helm release.
    /// </summary>
    public async Task InstallAsync(KindHelmChartResource resource, ILogger logger, CancellationToken cancellationToken)
    {
        var args = CreateInstallArguments(resource);

        logger.LogInformation(
            "Installing Helm chart '{ChartRef}' as release '{ReleaseName}' in cluster '{ClusterName}'...",
            resource.ChartRef, resource.ReleaseName, resource.Parent.Name);

        var result = await processRunner.RunAsync(
            logger,
            "helm",
            args,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to install Helm chart '{resource.ChartRef}' as release '{resource.ReleaseName}': {result.Error}");
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

        foreach (string valuesFile in resource.ValuesFiles)
        {
            arguments.Add("-f");
            arguments.Add(valuesFile);
        }

        return arguments;
    }
}
