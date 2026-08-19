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
    internal static async Task<string> GenerateConfigAsync(IKindResource resource, CancellationToken cancellationToken)
    {
        var configDir = Path.Combine(Path.GetTempPath(), "aspire-kind", resource.Name);
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "kind-config.yaml");

        var config = new KindConfigModel();
        config.Nodes.Add(new KindNodeModel { Role = "control-plane" });

        // Apply user-supplied config customizations (composed via annotations).
        foreach (var annotation in resource.Annotations.OfType<KindConfigAnnotation>())
        {
            annotation.Configure(config);
        }

        // Add worker nodes after config callbacks so the count is independent of call order.
        if (resource.TryGetLastAnnotation<WorkerNodesAnnotation>(out var workersAnnotation))
        {
            for (int i = 0; i < workersAnnotation.Count; i++)
            {
                config.Nodes.Add(new KindNodeModel { Role = "worker" });
            }
        }

        // Apply Kubernetes version after all config callbacks so every node gets the image,
        // regardless of the order WithKubernetesVersion and WithWorkerNodes/WithKindConfig were called.
        if (resource.TryGetLastAnnotation<KindNodeImageAnnotation>(out var imageAnnotation) &&
            imageAnnotation.Image is not null)
        {
            foreach (var node in config.Nodes)
            {
                node.Image ??= imageAnnotation.Image;
            }
        }
        else if (resource.TryGetLastAnnotation<KindNodeImageAnnotation>(out imageAnnotation) &&
                 imageAnnotation.Version is not null)
        {
            var image = $"{imageAnnotation.Registry}:{imageAnnotation.Version}";
            foreach (var node in config.Nodes)
            {
                node.Image ??= image;
            }
        }

        if (resource.TryGetLastAnnotation<KindNodeMountsAnnotation>(out var mountsAnnotation) &&
            mountsAnnotation.Mounts.Count > 0)
        {
            foreach (var node in config.Nodes)
            {
                node.ExtraMounts ??= [];

                foreach (var mount in mountsAnnotation.Mounts)
                {
                    node.ExtraMounts.Add(new KindMountModel
                    {
                        HostPath = mount.HostPath,
                        ContainerPath = mount.ContainerPath,
                        ReadOnly = mount.ReadOnly,
                        SelinuxRelabel = mount.SelinuxRelabel,
                        Propagation = mount.Propagation,
                    });
                }
            }
        }

        var yaml = s_serializer.Serialize(config);
        await File.WriteAllTextAsync(configPath, yaml, cancellationToken).ConfigureAwait(false);
        return configPath;
    }
}
