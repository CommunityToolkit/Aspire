using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Solr;

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
    /// <returns>A reference to the <see cref="IResourceBuilder{SolrResource}"/>.</returns>
    public static IResourceBuilder<SolrResource> AddSolr(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resource = new SolrResource(name);
        return builder.AddResource(resource)
                     .WithImage(SolrContainerImageTags.Image, SolrContainerImageTags.Tag)
                     .WithImageRegistry(SolrContainerImageTags.Registry)
                     .WithHttpEndpoint(targetPort: 8983, port: port, name: SolrResource.PrimaryEndpointName)
                     .WithArgs("solr-precreate", "solr");
    }

    /// <summary>
    /// Adds a Solr core to the Solr resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SolrResource}"/>.</param>
    /// <param name="coreName">The name of the Solr core.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{SolrCoreResource}"/>.</returns>
    public static IResourceBuilder<SolrCoreResource> AddSolrCore(this IResourceBuilder<SolrResource> builder, [ResourceName] string coreName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(coreName);

        var core = new SolrCoreResource(coreName, builder.Resource);
        builder.Resource.AddCore(core);
        return builder.ApplicationBuilder.AddResource(core);
    }
}
