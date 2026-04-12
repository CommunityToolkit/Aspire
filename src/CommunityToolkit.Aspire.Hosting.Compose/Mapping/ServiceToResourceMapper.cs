using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Compose.Mapping;

/// <summary>
/// Maps Docker Compose services to Aspire container resources.
/// </summary>
internal static class ServiceToResourceMapper
{
    /// <summary>
    /// Maps all services from a parsed compose file to Aspire resources.
    /// </summary>
    public static Dictionary<string, IResourceBuilder<ContainerResource>> MapServices(IDistributedApplicationBuilder builder, ComposeFile composeFile, string composePath, ILogger? logger = null)
    {
        Dictionary<string, IResourceBuilder<ContainerResource>> resources = new(StringComparer.OrdinalIgnoreCase);
        string composeDir = Path.GetDirectoryName(Path.GetFullPath(composePath)) ?? ".";

        foreach ((string serviceName, ComposeService service) in composeFile.Services)
        {
            if (service.Image is null && service.Build is null)
            {
                logger?.LogWarning("Compose service '{ServiceName}' has no image or build configuration. Skipping.", serviceName);
                continue;
            }

            resources[serviceName] = MapService(builder, serviceName, service, composeDir, logger);
        }

        DependsOnMapper.Apply(composeFile, resources);

        return resources;
    }

    private static IResourceBuilder<ContainerResource> MapService(IDistributedApplicationBuilder builder, string serviceName, ComposeService service, string composeDir, ILogger? logger)
    {
        (string image, string tag) = ParseImageReference(service.Image ?? string.Empty);
        IResourceBuilder<ContainerResource> resourceBuilder = builder.AddContainer(serviceName, image, tag);

        PortMapper.Map(resourceBuilder, service, serviceName, logger);
        EnvironmentMapper.Map(resourceBuilder, service);
        VolumeMapper.Map(resourceBuilder, service, composeDir);
        CommandMapper.Map(resourceBuilder, service);
        HealthcheckMapper.Map(resourceBuilder, service, serviceName, builder);

        if (service.ContainerName is { } containerName)
            resourceBuilder.WithAnnotation(new ContainerNameAnnotation { Name = containerName });

        return resourceBuilder;
    }

    internal static (string Image, string Tag) ParseImageReference(string imageRef)
    {
        if (string.IsNullOrEmpty(imageRef))
            return (ComposeConstants.Defaults.ScratchImage, ComposeConstants.Defaults.LatestTag);

        int lastColon = imageRef.LastIndexOf(':');

        return lastColon > 0 && !imageRef[(lastColon + 1)..].Contains('/')
            ? (imageRef[..lastColon], imageRef[(lastColon + 1)..])
            : (imageRef, ComposeConstants.Defaults.LatestTag);
    }

    internal static string[] ParseStringOrList(object value) =>
        value switch
        {
            string str => str.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            List<object> list => [.. list.Select(item => item.ToString()!)],
            _ => []
        };
}
