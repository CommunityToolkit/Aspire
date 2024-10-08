using Aspire.Hosting.ApplicationModel;
using HealthChecks.Uris;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <remarks>
/// This has been copied from the David Fowler davidfowl/WaitForDependenciesAspire project and will likely be removed in the future.
/// </remarks>
[Experimental("CTASPIRE009", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
public static class HttpEndpointHealthCheckExtensions
{
    /// <summary>
    /// Adds a health check to the resource with HTTP endpoints.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="endpointName">The optional name of the endpoint. If not specified, will be the first http or https endpoint (based on scheme).</param>
    /// <param name="path">path to send the HTTP request to. This will be appended to the base URL of the resolved endpoint.</param>
    /// <param name="configure">A callback to configure the options for this health check.</param>
    public static IResourceBuilder<T> WithHealthCheck<T>(
        this IResourceBuilder<T> builder, 
        string? endpointName = null,  
        string path = "health", 
        Action<UriHealthCheckOptions>? configure = null)
        where T : IResourceWithEndpoints
    {
        return builder.WithAnnotation(new HealthCheckAnnotation(async (resource, ct) =>
        {
            if (resource is not IResourceWithEndpoints resourceWithEndpoints)
            {
                return null;
            }

            var endpoint = endpointName is null
             ? resourceWithEndpoints.GetEndpoints().FirstOrDefault(e => e.Scheme is "http" or "https")
             : resourceWithEndpoints.GetEndpoint(endpointName);

            var url = endpoint?.Url;

            if (url is null)
            {
                return null;
            }

            var options = new UriHealthCheckOptions();

            options.AddUri(new(new(url), path));

            configure?.Invoke(options);

            var client = new HttpClient();
            return new UriHealthCheck(options, () => client);
        }));
    }
}
