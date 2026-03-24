// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Generates Kind cluster configuration YAML files.
/// </summary>
internal static class KindConfigGenerator
{
    private const string KindNodeImageRepository = "kindest/node";

    /// <summary>
    /// Generates a Kind configuration file and returns its path.
    /// The caller is responsible for deleting the file when no longer needed.
    /// </summary>
    internal static string GenerateConfig(KindClusterResource resource)
    {
        var configDir = Path.Combine(Path.GetTempPath(), "aspire-kind", resource.Name);
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "kind-config.yaml");

        var yaml = new StringBuilder();
        yaml.AppendLine("kind: Cluster");
        yaml.AppendLine("apiVersion: kind.x-k8s.io/v1alpha4");
        yaml.AppendLine("nodes:");

        AppendNode(yaml, "control-plane", resource.KubernetesVersion);

        for (int i = 0; i < resource.WorkerNodes; i++)
        {
            AppendNode(yaml, "worker", resource.KubernetesVersion);
        }

        File.WriteAllText(configPath, yaml.ToString());
        return configPath;
    }

    private static void AppendNode(StringBuilder yaml, string role, string? kubernetesVersion)
    {
        yaml.AppendLine($"- role: {role}");

        if (!string.IsNullOrEmpty(kubernetesVersion))
        {
            yaml.AppendLine($"  image: {KindNodeImageRepository}:{kubernetesVersion}");
        }
    }
}
