using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Vercel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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
                $"Vercel endpoint references support only HTTP or HTTPS endpoints. Endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' uses scheme '{endpoint.UriScheme}'.");
        }

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

        return property switch
        {
            EndpointProperty.Url => ReferenceExpression.Create($"https://{host}"),
            EndpointProperty.Host or EndpointProperty.IPV4Host => host,
            EndpointProperty.Port => ReferenceExpression.Create($"{port.ToString(CultureInfo.InvariantCulture)}"),
            EndpointProperty.TargetPort => endpoint.TargetPort is int targetPort
                ? ReferenceExpression.Create($"{targetPort.ToString(CultureInfo.InvariantCulture)}")
                : throw new InvalidOperationException(
                    $"The endpoint property '{EndpointProperty.TargetPort}' cannot be resolved for endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' because the endpoint does not define an explicit target port."),
            EndpointProperty.Scheme => ReferenceExpression.Create($"https"),
            EndpointProperty.HostAndPort => ReferenceExpression.Create($"{host}:{port.ToString(CultureInfo.InvariantCulture)}"),
            EndpointProperty.TlsEnabled => ReferenceExpression.Create($"{bool.TrueString}"),
            _ => throw new InvalidOperationException($"The property '{property}' is not supported for the endpoint '{endpoint.Name}'.")
        };
    }

    private static bool IsHttpEndpoint(EndpointAnnotation endpoint)
        => string.Equals(endpoint.UriScheme, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(endpoint.UriScheme, "https", StringComparison.OrdinalIgnoreCase);
}
