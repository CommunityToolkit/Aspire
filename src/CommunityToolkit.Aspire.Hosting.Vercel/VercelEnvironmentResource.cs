using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Vercel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Vercel deployment target for Aspire workloads that publish as Dockerfile builds.
/// </summary>
/// <param name="name">The Aspire resource name for the Vercel environment.</param>
[Experimental("CTASPIREVERCEL001")]
[AspireExport(ExposeProperties = true)]
public sealed class VercelEnvironmentResource(string name) : Resource(name), IComputeEnvironmentResource
{
    /// <inheritdoc/>
    [Experimental("ASPIRECOMPUTE002", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    public ReferenceExpression GetHostAddressExpression(EndpointReference endpointReference)
    {
        ArgumentNullException.ThrowIfNull(endpointReference);

        var options = this.GetVercelOptions();
        if (!options.Production)
        {
            throw new InvalidOperationException(
                $"Vercel endpoint references require production deployments because preview and custom target URLs are assigned by Vercel after deployment. Call {nameof(VercelEnvironmentResourceBuilderExtensions.WithVercelProductionDeployments)} on the Vercel environment, or remove the reference.");
        }

        var endpoint = endpointReference.EndpointAnnotation;
        if (!endpoint.IsExternal)
        {
            throw new InvalidOperationException(
                $"Vercel endpoint references can only target external HTTP or HTTPS endpoints. Endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' is not external.");
        }

        if (!IsHttpEndpoint(endpoint))
        {
            throw new InvalidOperationException(
                $"Vercel endpoint references support only HTTP or HTTPS endpoints with HTTP transports. Endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' uses scheme '{endpoint.UriScheme}' and transport '{endpoint.Transport}'.");
        }

        // Same-deploy references need an address that exists before `vercel deploy`
        // finishes. Production project aliases are deterministic; preview URLs are not.
        string projectName = VercelDeploymentStep.GetVercelProjectName(endpointReference.Resource);
        return ReferenceExpression.Create($"{projectName}.vercel.app");
    }

    /// <inheritdoc/>
    [Experimental("ASPIRECOMPUTE002", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    public ReferenceExpression GetEndpointPropertyExpression(EndpointReferenceExpression endpointReferenceExpression)
    {
        ArgumentNullException.ThrowIfNull(endpointReferenceExpression);

        var endpointReference = endpointReferenceExpression.Endpoint;
        var property = endpointReferenceExpression.Property;
        var endpoint = endpointReference.EndpointAnnotation;
        var host = GetHostAddressExpression(endpointReference);
        const int port = 443;

        // These expressions model the caller-visible Vercel edge endpoint, not the
        // container listener. Vercel terminates TLS and forwards to the runtime's $PORT,
        // so service references use HTTPS/443 while TargetPort remains container metadata.
        return property switch
        {
            // Vercel terminates public HTTPS at the platform edge, so callers always use
            // the alias over 443 even when the container listens on $PORT internally.
            EndpointProperty.Url => ReferenceExpression.Create($"https://{host}"),
            EndpointProperty.Host or EndpointProperty.IPV4Host => host,
            EndpointProperty.Port => ReferenceExpression.Create($"{port.ToString(CultureInfo.InvariantCulture)}"),
            EndpointProperty.TargetPort => endpoint.TargetPort is int targetPort
                ? ReferenceExpression.Create($"{targetPort.ToString(CultureInfo.InvariantCulture)}")
                : throw new InvalidOperationException(
                    // Azure publishers can emit ContainerPortReference placeholders into
                    // Bicep/Helm. Vercel deploy receives concrete CLI env values, so an
                    // unresolved TargetPort would become a bogus string rather than a
                    // target-native reference.
                    $"The endpoint property '{EndpointProperty.TargetPort}' cannot be resolved for endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' because the endpoint does not define an explicit target port."),
            EndpointProperty.Scheme => ReferenceExpression.Create($"https"),
            EndpointProperty.HostAndPort => ReferenceExpression.Create($"{host}:{port.ToString(CultureInfo.InvariantCulture)}"),
            EndpointProperty.TlsEnabled => ReferenceExpression.Create($"{bool.TrueString}"),
            _ => throw new InvalidOperationException($"The property '{property}' is not supported for the endpoint '{endpoint.Name}'.")
        };
    }

    private static bool IsHttpEndpoint(EndpointAnnotation endpoint)
        => endpoint.Protocol == ProtocolType.Tcp
            && (string.Equals(endpoint.UriScheme, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.UriScheme, "https", StringComparison.OrdinalIgnoreCase))
            && (string.Equals(endpoint.Transport, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.Transport, "http2", StringComparison.OrdinalIgnoreCase));
}
