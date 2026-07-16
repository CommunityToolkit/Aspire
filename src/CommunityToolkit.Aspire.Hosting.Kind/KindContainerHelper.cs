// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.KubeConfigModels;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal static class KindContainerHelper
{
    /// <summary>
    /// Generates a Kind kubeconfig file that other containers can use to reach the control plane over the Kind network.
    /// </summary>
    /// <param name="resource">The Kind cluster resource.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the rewritten kubeconfig has been written.</returns>
    public static async Task GenerateContainerKubeconfigAsync(KindClusterResource resource, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var content = await File.ReadAllTextAsync(resource.KubeconfigPath, ct).ConfigureAwait(false);
        var rewrittenContent = RewriteClusterEndpointForContainerAccess(content, resource.Name);

        Directory.CreateDirectory(Path.GetDirectoryName(resource.ContainerKubeconfigPath)!);
        await File.WriteAllTextAsync(resource.ContainerKubeconfigPath, rewrittenContent, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Rewrites a Kind kubeconfig so other containers can reach the control plane over the Kind network.
    /// </summary>
    /// <param name="kubeconfigYaml">The original kubeconfig YAML.</param>
    /// <param name="clusterName">The Kind cluster resource name.</param>
    /// <returns>The rewritten kubeconfig YAML.</returns>
    internal static string RewriteClusterEndpointForContainerAccess(string kubeconfigYaml, string clusterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(kubeconfigYaml);
        ArgumentException.ThrowIfNullOrEmpty(clusterName);

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(kubeconfigYaml));
        K8SConfiguration kubeConfig = KubernetesClientConfiguration.LoadKubeConfig(stream);

        string targetClusterName = ResolveTargetClusterName(kubeConfig, clusterName);
        Cluster targetCluster = kubeConfig.Clusters.First(cluster => string.Equals(cluster.Name, targetClusterName, StringComparison.Ordinal));

        targetCluster.ClusterEndpoint.Server = $"https://{clusterName}-control-plane:6443";
        targetCluster.ClusterEndpoint.SkipTlsVerify = true;
        targetCluster.ClusterEndpoint.CertificateAuthorityData = null;

        return KubernetesYaml.Serialize(kubeConfig);
    }

    private static string ResolveTargetClusterName(K8SConfiguration kubeConfig, string clusterName)
    {
        string? currentContextName = kubeConfig.CurrentContext;
        if (!string.IsNullOrWhiteSpace(currentContextName))
        {
            Context? currentContext = kubeConfig.Contexts.FirstOrDefault(context => string.Equals(context.Name, currentContextName, StringComparison.Ordinal));
            if (currentContext?.ContextDetails?.Cluster is { Length: > 0 } contextClusterName)
            {
                return contextClusterName;
            }
        }

        string inferredClusterName = $"kind-{clusterName}";
        Cluster? matchingCluster = kubeConfig.Clusters.FirstOrDefault(cluster => string.Equals(cluster.Name, inferredClusterName, StringComparison.Ordinal));
        if (matchingCluster is not null)
        {
            return matchingCluster.Name;
        }

        throw new InvalidOperationException($"Could not determine the kubeconfig cluster entry for Kind cluster '{clusterName}'.");
    }
}
