using System.Globalization;

using Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder.Utils;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

/// <summary>
/// Provides extension methods for adding DataApiBuilder api to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DataApiBuilderHostingExtension
{
    /// <summary>
    /// Adds a DataApiBuilder api to the application model. Executes the containerized DataApiBuilder api.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">The <see cref="DataApiBuilderContainerResourceOptions"/> to configure the DataApiBuilder api.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder, string name, DataApiBuilderContainerResourceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ContainerImageName) == true)
        {
            throw new ArgumentNullException("Container image name must be specified.", nameof(options));
        }

        var resource = new DataApiBuilderContainerResource(name);

        var rb = builder.AddResource(resource)
            .WithAnnotation(new ContainerImageAnnotation { Image = options.ContainerImageName, Tag = options.ContainerImageTag, Registry = options.ContainerRegistry })
            .WithHttpEndpoint(port: options.Port, targetPort: options.TargetPort, name: DataApiBuilderContainerResource.HttpEndpointName)
            .WithDataApiBuilderDefaults(options);

        if(string.IsNullOrWhiteSpace(options.ConfigFilePath))
        {
            throw new ArgumentException("The DataApiBuilder configuration file must be specified.", nameof(options));
        }
        rb.WithBindMount(PathNormalizer.NormalizePathForCurrentPlatform(options.ConfigFilePath), "/App/dab-config.json", true);

        return rb;
    }

    /// <summary>
    /// Adds a DataAPIBuilder application to the application model. Executes the containerized DataAPIBuilder app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configFilePath">The path to the config file for Data API Builder.</param>"
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
        var options = new DataApiBuilderContainerResourceOptions
        {
            ContainerRegistry = containerRegistry,
            ContainerImageName = containerImageName,
            ContainerImageTag = containerImageTag,
            Port = port,
            TargetPort = targetPort,
            ConfigFilePath = configFilePath
        };
        return builder.AddDataAPIBuilder(name, options);
    }

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder,
        DataApiBuilderContainerResourceOptions options) =>
        builder.WithOtlpExporter();
}
