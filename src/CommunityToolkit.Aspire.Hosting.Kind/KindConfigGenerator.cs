// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Generates Kind cluster configuration YAML files.
/// </summary>
internal static class KindConfigGenerator
{
    private static readonly ISerializer s_serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    /// <summary>
    /// Generates a Kind configuration file and returns its path.
    /// The caller is responsible for deleting the file when no longer needed.
    /// </summary>
    internal static async Task<string> GenerateConfigAsync(KindClusterResource resource, CancellationToken cancellationToken)
    {
        var configDir = Path.Combine(Path.GetTempPath(), "aspire-kind", resource.Name);
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "kind-config.yaml");

        var config = new KindConfigModel();

        var controlPlane = new KindNodeModel { Role = "control-plane" };
        if (!string.IsNullOrEmpty(resource.KubernetesVersion))
        {
            controlPlane.Image = $"{KindContainerImageTags.KindNodeImageRepository}:{resource.KubernetesVersion}";
        }

        config.Nodes.Add(controlPlane);

        for (int i = 0; i < resource.WorkerNodes; i++)
        {
            var worker = new KindNodeModel { Role = "worker" };
            if (!string.IsNullOrEmpty(resource.KubernetesVersion))
            {
                worker.Image = $"{KindContainerImageTags.KindNodeImageRepository}:{resource.KubernetesVersion}";
            }

            config.Nodes.Add(worker);
        }

        var yaml = s_serializer.Serialize(config);
        await File.WriteAllTextAsync(configPath, yaml, cancellationToken).ConfigureAwait(false);
        return configPath;
    }
}

/// <summary>
/// Represents a Kind cluster configuration document.
/// </summary>
internal sealed class KindConfigModel
{
    /// <summary>
    /// Gets or sets the API version for the Kind configuration.
    /// </summary>
    public string ApiVersion { get; set; } = "kind.x-k8s.io/v1alpha4";

    /// <summary>
    /// Gets or sets the resource kind.
    /// </summary>
    public string Kind { get; set; } = "Cluster";

    /// <summary>
    /// Gets the list of nodes in the cluster.
    /// </summary>
    public List<KindNodeModel> Nodes { get; } = [];
}

/// <summary>
/// Represents a node in a Kind cluster configuration.
/// </summary>
internal sealed class KindNodeModel
{
    /// <summary>
    /// Gets or sets the role of the node (e.g., "control-plane" or "worker").
    /// </summary>
    public string Role { get; set; } = "control-plane";

    /// <summary>
    /// Gets or sets the container image for the node.
    /// When <see langword="null"/>, Kind uses its default image.
    /// </summary>
    public string? Image { get; set; }
}
