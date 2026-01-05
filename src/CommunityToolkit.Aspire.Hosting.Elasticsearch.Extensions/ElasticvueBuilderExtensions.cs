using System.Text.Json;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for Elasticvue resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class ElasticvueBuilderExtensions
{
    /// <summary>
    /// Configures the host port that the Elasticvue resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for Elasticvue.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The resource builder for Elasticvue.</returns>
    public static IResourceBuilder<ElasticvueContainerResource> WithHostPort(this IResourceBuilder<ElasticvueContainerResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(ElasticvueContainerResource.PrimaryEndpointName, endpoint =>
        {
            endpoint.Port = port;
        });
    }

    /// <summary>
    /// Adds a Elasticvue container resource to the application.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <remarks>
    /// Multiple <see cref="AddElasticvue(IDistributedApplicationBuilder, string, int?)"/> calls will return the same resource builder instance.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    internal static IResourceBuilder<ElasticvueContainerResource> AddElasticvue(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        if (builder.Resources.OfType<ElasticvueContainerResource>().SingleOrDefault() is { } existingElasticVueResource)
        {
            var builderForExistingResource = builder.CreateResourceBuilder(existingElasticVueResource);
            return builderForExistingResource;
        }
        else
        {
            var elasticVueContainer = new ElasticvueContainerResource(name);
            var elasticVueContainerBuilder = builder.AddResource(elasticVueContainer)
                                                .WithImage(ElasticvueContainerImageTags.Image, ElasticvueContainerImageTags.Tag)
                                                .WithImageRegistry(ElasticvueContainerImageTags.Registry)
                                                .WithHttpEndpoint(targetPort: 8080, port: port, name: ElasticvueContainerResource.PrimaryEndpointName)
                                                .WithUrlForEndpoint(ElasticvueContainerResource.PrimaryEndpointName, e => e.DisplayText = "Elasticvue Dashboard")
                                                .ExcludeFromManifest();

            return elasticVueContainerBuilder;
        }
    }
    /// <summary>
    /// Adds an administration and development platform for Elasticsearch to the application model using Elasticvue.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="ElasticvueContainerImageTags.Tag"/> tag of the <inheritdoc cref="ElasticvueContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The Elasticsearch server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for Elasticvue container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <example>
    /// Use in application host with a Elasticsearch resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var elasticsearch = builder.AddElasticsearch("elasticsearch")
    ///    .WithElasticvue();
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(elasticsearch);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ElasticsearchResource> WithElasticvue(this IResourceBuilder<ElasticsearchResource> builder, Action<IResourceBuilder<ElasticvueContainerResource>>? configureContainer = null, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"elasticvue";

        var elasticvueBuilder = AddElasticvue(builder.ApplicationBuilder, containerName);

        elasticvueBuilder
            .WithEnvironment(context => ConfigureElasticvueContainer(context, builder.ApplicationBuilder));

        configureContainer?.Invoke(elasticvueBuilder);

        // Enable CORS for Elasticsearch instance so that
        // Elasticvue can connect to it
        builder.WithEnvironment("http.cors.enabled", "true")
            .WithEnvironment("http.cors.allow-origin", () => $"\"{elasticvueBuilder.Resource.PrimaryEndpoint.Url}\"");

        return builder;
    }

    private static async Task ConfigureElasticvueContainer(EnvironmentCallbackContext context, IDistributedApplicationBuilder applicationBuilder)
    {
        var elasticsearchInstances = applicationBuilder.Resources.OfType<ElasticsearchResource>();

        var aspireClusters = new List<ElasticvueEnvironmentSettings>();
        foreach (var elasticsearchResource in elasticsearchInstances)
        {
            aspireClusters.Add(new ElasticvueEnvironmentSettings(
                name: elasticsearchResource.Name,
                uri: elasticsearchResource.PrimaryEndpoint.Url,
                username: "elastic",
                password: (await elasticsearchResource.PasswordParameter.GetValueAsync(CancellationToken.None))!
            ));
        }

        var currentClustersSettings = context.EnvironmentVariables.GetValueOrDefault("ELASTICVUE_CLUSTERS")?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(currentClustersSettings))
        {
            currentClustersSettings = "[]"; // Initialize with an empty JSON object if not set
        }

        var currentClusters = JsonSerializer.Deserialize<List<ElasticvueEnvironmentSettings>>(currentClustersSettings) ?? throw new InvalidOperationException("ELASTICVUE_CLUSTERS environment variable deserialized to a null value.");
        foreach (var cluster in aspireClusters)
        {
            if (!currentClusters.Any(c => c.uri == cluster.uri))
            {
                currentClusters.Add(cluster);
            }
        }

        var clustersJson = JsonSerializer.Serialize(currentClusters);
        context.EnvironmentVariables["ELASTICVUE_CLUSTERS"] = clustersJson;
    }

}

internal record ElasticvueEnvironmentSettings(string name, string uri, string username, string password);
