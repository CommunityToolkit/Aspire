using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder;

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
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        params string[] configFilePaths)
    {
        return builder.AddDataAPIBuilder(name, null, configFilePaths);
    }

    /// <summary>
    /// Adds a DataAPIBuilder application to the application model. Executes the containerized DataAPIBuilder app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">The port number for the Data API Builder container.</param>"
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        params string[] configFilePaths)
    {
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));

        var resource = new DataApiBuilderContainerResource(name);

        var rb = builder.AddResource(resource)
            .WithAnnotation(new ContainerImageAnnotation
            {
                Image = DataApiBuilderContainerImageTags.Image,
                Tag = DataApiBuilderContainerImageTags.Tag,
                Registry = DataApiBuilderContainerImageTags.Registry
            })
            .WithHttpEndpoint(port: port, targetPort: 5000, name: DataApiBuilderContainerResource.HttpEndpointName)
            .WithDataApiBuilderDefaults();

        // Use default config file path if no paths are provided
        if (configFilePaths is [])
        {
            configFilePaths = ["./dab-config.json"];
        }

        foreach (var configFilePath in configFilePaths)
        {
            var configFileName = File.Exists(configFilePath)
                ? Path.GetFileName(configFilePath)
                : throw new FileNotFoundException($"Config file not found: {configFilePath}");

            rb.WithBindMount(configFilePath, $"/App/{configFileName}", true);
        }

        return rb;
    }

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder) =>
        builder.WithOtlpExporter();
}

