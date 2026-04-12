using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Compose.Mapping;

/// <summary>
/// Maps compose port definitions to Aspire endpoints.
/// </summary>
internal static class PortMapper
{
    public static void Map(IResourceBuilder<ContainerResource> resourceBuilder, ComposeService service, string serviceName, ILogger? logger)
    {
        if (service.Ports is not { } ports)
            return;

        int endpointIndex = 0;

        foreach (string portMapping in ports)
        {
            if (ParsePortMapping(portMapping) is not { } parsed)
            {
                logger?.LogWarning("Could not parse port mapping '{PortMapping}'. Skipping.", portMapping);
                continue;
            }

            (int? hostPort, int containerPort, string protocol) = parsed;
            string scheme = protocol == ComposeConstants.Protocol.Udp ? ComposeConstants.Protocol.Udp : containerPort is 443 or 8443 ? ComposeConstants.Protocol.Https : ComposeConstants.Protocol.Http;
            string endpointName = $"{serviceName}-{protocol}-{containerPort}-{endpointIndex++}";
            resourceBuilder.WithEndpoint(port: hostPort, targetPort: containerPort, name: endpointName, scheme: scheme);
        }
    }

    internal static (int? HostPort, int ContainerPort, string Protocol)? ParsePortMapping(string mapping)
    {
        string protocol = ComposeConstants.Protocol.Tcp;
        string portPart = mapping;

        if (mapping.IndexOf('/') is var slashIndex and >= 0)
        {
            protocol = mapping[(slashIndex + 1)..];
            portPart = mapping[..slashIndex];
        }

        string[] parts = portPart.Split(':');

        return parts.Length switch
        {
            1 when int.TryParse(parts[0], out int p) => (null, p, protocol),
            2 when int.TryParse(parts[0], out int hp) && int.TryParse(parts[1], out int cp) => (hp, cp, protocol),
            3 when int.TryParse(parts[2], out int cp) => (ParseOptionalInt(parts[1]), cp, protocol),
            _ => null
        };
    }

    private static int? ParseOptionalInt(string value) =>
        string.IsNullOrEmpty(value) ? null : int.TryParse(value, out int result) ? result : null;
}
