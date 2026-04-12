using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

namespace CommunityToolkit.Aspire.Hosting.Compose.Mapping;

/// <summary>
/// Maps compose volume definitions to Aspire volumes and bind mounts.
/// </summary>
internal static class VolumeMapper
{
    public static void Map(IResourceBuilder<ContainerResource> resourceBuilder, ComposeService service, string composeDir)
    {
        if (service.Volumes is not { } volumes) 
            return;

        foreach (string volumeMapping in volumes)
        {
            string[] parts = volumeMapping.Split(':');

            if (parts.Length < 2)
            {
                resourceBuilder.WithVolume(volumeMapping);
                continue;
            }

            string source = parts[0];
            string target = parts[1];
            bool isReadOnly = parts.Length > 2 && parts[2] == ComposeConstants.Volume.ReadOnly;
            bool isPath = source.Length > 0 && source[0] switch
            {
                '.' or '/' => true,
                _ => source.Contains(Path.DirectorySeparatorChar) || source.Contains(Path.AltDirectorySeparatorChar)
            };

            if (isPath)
            {
                string absoluteSource = Path.IsPathRooted(source)
                    ? source
                    : Path.GetFullPath(Path.Combine(composeDir, source));
                resourceBuilder.WithBindMount(absoluteSource, target, isReadOnly);
            }
            else
            {
                resourceBuilder.WithVolume(source, target, isReadOnly);
            }
        }
    }
}
