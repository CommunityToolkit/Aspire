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
        ArgumentException.ThrowIfNullOrEmpty(coreName);

        SolrResource resource = new(name, coreName);

        IResourceBuilder<SolrResource> solrBuilder = builder.AddResource(resource)
                     .WithImage(SolrContainerImageTags.Image, SolrContainerImageTags.Tag)
                     .WithImageRegistry(SolrContainerImageTags.Registry)
                     .WithHttpEndpoint(targetPort: 8983, port: port, name: SolrResource.PrimaryEndpointName)
                     .WithArgs(context =>
                     {
                         var configSetAnnotation = context.Resource.Annotations.OfType<SolrConfigSetAnnotation>().LastOrDefault();
                         if (configSetAnnotation is not null)
                         {
                             context.Args.Add("solr-create");
                             context.Args.Add("-c");
                             context.Args.Add(coreName);
                             context.Args.Add("-d");
                             context.Args.Add(configSetAnnotation.ConfigSetName);
                         }
                         else
                         {
                             context.Args.Add("solr-precreate");
                             context.Args.Add(coreName);
                         }
                     });

        string healthCheckKey = $"{name}_check";
        EndpointReference endpoint = solrBuilder.Resource.GetEndpoint(SolrResource.PrimaryEndpointName);

        builder.Services.AddHealthChecks()
            .AddUrlGroup(options =>
            {
                Uri uri = new(endpoint.Url);
                options.AddUri(new Uri(uri, $"solr/{coreName}/admin/ping"), setup => setup.ExpectHttpCode(200));
            }, healthCheckKey);

        solrBuilder.WithHealthCheck(healthCheckKey);

        return solrBuilder;
    }

    /// <summary>
    /// Specifies the path to the config set directory.
    /// </summary>
    /// <param name="builder">An <see cref="IResourceBuilder{T}"/> representing the Solr resource.</param>
    /// <param name="configSetName">The name of the config set.</param>
    /// <param name="configSetPath">Path to the config set directory.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> that can be used to further customize the resource.</returns>
    public static IResourceBuilder<SolrResource> WithConfigset(this IResourceBuilder<SolrResource> builder, string configSetName, string configSetPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(configSetName);
        ArgumentException.ThrowIfNullOrEmpty(configSetPath);

        builder.WithAnnotation(new SolrConfigSetAnnotation(configSetName, configSetPath));
        builder.WithBindMount(configSetPath, $"/opt/solr/server/solr/configsets/{configSetName}");
        return builder;
    }

     /// <summary>
    /// Adds a named volume for the data folder to a Solr container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SolrResource> WithDataVolume(this IResourceBuilder<SolrResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/solr", isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Solr container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SolrResource> WithDataBindMount(this IResourceBuilder<SolrResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/solr", isReadOnly);
    }
}