using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

namespace CommunityToolkit.Aspire.Hosting.Compose.Mapping;

/// <summary>
/// Maps compose command and entrypoint to Aspire args and entrypoint.
/// </summary>
internal static class CommandMapper
{
    public static void Map(IResourceBuilder<ContainerResource> resourceBuilder, ComposeService service)
    {
        MapCommand(resourceBuilder, service);
        MapEntrypoint(resourceBuilder, service);
    }

    private static void MapCommand(IResourceBuilder<ContainerResource> resourceBuilder, ComposeService service)
    {
        if (service.Command is not { } command)
            return;

        string[] args = ServiceToResourceMapper.ParseStringOrList(command);

        if (args.Length > 0)
            resourceBuilder.WithArgs(args);
    }

    private static void MapEntrypoint(IResourceBuilder<ContainerResource> resourceBuilder, ComposeService service)
    {
        if (service.Entrypoint is not { } entrypoint)
            return;

        string[] parts = ServiceToResourceMapper.ParseStringOrList(entrypoint);

        if (parts.Length == 0)
            return;

        resourceBuilder.WithEntrypoint(parts[0]);

        if (parts.Length > 1)
            resourceBuilder.WithArgs(parts[1..]);
    }
}
