using CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding DataApiBuilder api to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DataApiBuilderHostingExtension
{
    /// <summary>
    /// Adds a DataAPIBuilder application to the application model. Executes the containerized DataAPIBuilder app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configFilePath">The path to the config file for Data API Builder.</param>"
    /// <param name="port">The port number for the Data API Builder container.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string configFilePath = "./dab-config.json",
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));
        ArgumentNullException.ThrowIfNull("Config file path must be specified.", nameof(configFilePath));

        var resource = new DataApiBuilderContainerResource(name);

        var rb = builder.AddResource(resource)
            .WithAnnotation(new ContainerImageAnnotation { Image = DataApiBuilderContainerImageTags.Image, Tag = DataApiBuilderContainerImageTags.Tag, Registry = DataApiBuilderContainerImageTags.Registry })
            .WithHttpEndpoint(port: port, targetPort: 5000, name: DataApiBuilderContainerResource.HttpEndpointName)
            .WithDataApiBuilderDefaults();

        rb.WithBindMount(configFilePath, "/App/dab-config.json", true);

        return rb;
    }

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder) =>
        builder.WithOtlpExporter();
}
