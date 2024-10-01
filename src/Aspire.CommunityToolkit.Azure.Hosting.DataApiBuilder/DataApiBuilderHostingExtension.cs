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
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataApiBuilder(this IDistributedApplicationBuilder builder, string name, DataApiBuilderContainerResourceOptions options)
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
    /// <param name="options">The <see cref="DataApiBuilderContainerResourceOptions"/> to configure the DataApiBuilder api.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder, string name, DataApiBuilderContainerResourceOptions? options = null)
    {
        options ??= new DataApiBuilderContainerResourceOptions();
        return builder.AddDataApiBuilder(name, options);
    }

    /// <summary>
    /// Adds a DataAPIBuilder application to the application model. Executes the containerized DataAPIBuilder app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configFilePath">The path to the config file for Data API Builder.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder, string name, string configFilePath)
    {
        var options = new DataApiBuilderContainerResourceOptions
        {
            ConfigFilePath = configFilePath
        };
        return builder.AddDataAPIBuilder(name, options);
    }

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder,
        DataApiBuilderContainerResourceOptions options) =>
        builder.WithOtlpExporter();
}
