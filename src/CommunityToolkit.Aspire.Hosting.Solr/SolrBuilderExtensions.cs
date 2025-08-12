using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Solr;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding and configuring a Solr resource.
/// </summary>
public static class SolrBuilderExtensions
{
    /// <summary>
    /// Adds an Apache Solr container resource to the distributed application.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for Solr.</param>
    /// <param name="coreName">The name of the core to create.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{SolrResource}"/>.</returns>
    public static IResourceBuilder<SolrResource> AddSolr(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null, string? coreName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if(string.IsNullOrEmpty(coreName))
        {
            coreName = "solr";
        }   

        var resource = new SolrResource(name, coreName);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (_, ct) =>
        {
            connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{resource.Name}' resource but the connection string was null.");
            }
        });

        string healthCheckKey = $"{name}_check";
        // cache the HttpClient so it is reused on subsequent calls to the health check
        HttpClient? httpClient = null;
        builder.Services.AddHealthChecks().AddTypeActivatedCheck<CommunityToolkit.Aspire.Hosting.Solr.SolrHealthCheck>(healthCheckKey, args: [
            () => {
                // NOTE: Ensure that execution of this setup callback is deferred until after
                //       the container is built & started.
                return httpClient ??= CreateHttpClient(connectionString!, coreName);

                static HttpClient CreateHttpClient(string connectionString, string coreName)
                {
                    // The connection string already points to the core endpoint, 
                    // we need to get the base Solr URL for the admin ping
                    var baseUrl = connectionString.Replace($"/solr/{coreName}", "");
                    
                    var client = new HttpClient
                    {
                        BaseAddress = new Uri(baseUrl),
                        Timeout = TimeSpan.FromSeconds(5)
                    };
                    
                    return client;
                }
            },
            coreName
        ]);

        return builder.AddResource(resource)
                     .WithImage(SolrContainerImageTags.Image, SolrContainerImageTags.Tag)
                     .WithImageRegistry(SolrContainerImageTags.Registry)
                     .WithHttpEndpoint(targetPort: 8983, port: port, name: SolrResource.PrimaryEndpointName)
                     .WithArgs("solr-precreate", coreName)
                     .WithHealthCheck(healthCheckKey);
    }
}
