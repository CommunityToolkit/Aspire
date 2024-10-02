using Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder.Utils;
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
    /// <param name="containerRegistry">The container registry for the Data API Builder image.</param>"
    /// <param name="containerImageName">The name of the Data API Builder image.</param>"
    /// <param name="containerImageTag">The tag of the Data API Builder image.</param>"
    /// <param name="port">The port number for the Data API Builder container.</param>"
    /// <param name="targetPort">The target port number for the Data API Builder container.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder, 
        string name, 
        string configFilePath = "./dab-config.json", 
        string containerRegistry = "mcr.microsoft.com", 
        string containerImageName = "azure-databases/data-api-builder",
        string containerImageTag = "latest",
        int port = 5000,
        int targetPort = 5000)
    {
        if (string.IsNullOrWhiteSpace(containerImageName) == true)
        {
            throw new ArgumentNullException("Container image name must be specified.", nameof(containerImageName));
        }

        var resource = new DataApiBuilderContainerResource(name);

        var rb = builder.AddResource(resource)
            .WithAnnotation(new ContainerImageAnnotation { Image = containerImageName, Tag = containerImageTag, Registry = containerRegistry })
            .WithHttpEndpoint(port: port, targetPort: targetPort, name: DataApiBuilderContainerResource.HttpEndpointName)
            .WithDataApiBuilderDefaults();

        if(string.IsNullOrWhiteSpace(configFilePath))
        {
            throw new ArgumentException("The DataApiBuilder configuration file must be specified.", nameof(configFilePath));
        }
        rb.WithBindMount(PathNormalizer.NormalizePathForCurrentPlatform(configFilePath), "/App/dab-config.json", true);

        return rb;
    }

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder) =>
        builder.WithOtlpExporter();
}
