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

        coreName ??= "solr";

        var resource = new SolrResource(name, coreName);

        var solrBuilder = builder.AddResource(resource)
                     .WithImage(SolrContainerImageTags.Image, SolrContainerImageTags.Tag)
                     .WithImageRegistry(SolrContainerImageTags.Registry)
                     .WithHttpEndpoint(targetPort: 8983, port: port, name: SolrResource.PrimaryEndpointName)
                     .WithArgs("solr-precreate", coreName);

        string healthCheckKey = $"{name}_check";
        var endpoint = solrBuilder.Resource.GetEndpoint(SolrResource.PrimaryEndpointName);

        builder.Services.AddHealthChecks()
            .AddUrlGroup(options =>
            {
                var uri = new Uri(endpoint.Url);
                options.AddUri(new Uri(uri, $"solr/{coreName}/admin/ping"), setup => setup.ExpectHttpCode(200));
            }, healthCheckKey);

        solrBuilder.WithHealthCheck(healthCheckKey);

        return solrBuilder;
    }
}
