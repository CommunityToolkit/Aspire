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
            throw new ArgumentException("Container image name must be specified.", nameof(options));
        }

        var resource = new DataApiBuilderContainerResource(name);

        var rb = builder.AddResource(resource);
        if (string.IsNullOrWhiteSpace(options.ContainerRegistry) == false)
        {
            rb.WithImageRegistry(options.ContainerRegistry);
        }
        rb.WithImage(options.ContainerImageName)
          .WithImageTag(options.ContainerImageTag)
          .WithHttpEndpoint(port: options.Port, targetPort: options.TargetPort, name: DataApiBuilderContainerResource.HttpEndpointName)
          .WithDataApiBuilderDefaults(options);

        if(string.IsNullOrWhiteSpace(options.ConfigFilePath) == false)
        {
            throw new ArgumentException("The DataApiBuilder configuration file must be specified.", nameof(options));
        }
        rb.WithVolume(PathNormalizer.NormalizePathForCurrentPlatform(options.ConfigFilePath), "/App/dab-config.json");

        return rb;
    }

    /// <summary>
    /// Adds a Spring application to the application model. Executes the containerized Spring app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">The <see cref="DataApiBuilderContainerResourceOptions"/> to configure the DataApiBuilder api.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddSpringApp(this IDistributedApplicationBuilder builder, string name, DataApiBuilderContainerResourceOptions options)
    {
        return builder.AddDataApiBuilder(name, options);
    }

    /// <summary>
    /// Adds a DataApiBuilder api to the application model. Executes the executable DataApiBuilder api.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="options">The <see cref="DataApiBuilderExecutableResourceOptions"/> to configure the DataApiBuilder api.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderExecutableResource> AddDataApiBuilder(this IDistributedApplicationBuilder builder, string name, string workingDirectory, DataApiBuilderExecutableResourceOptions options)
    {
        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));
        var resource = new DataApiBuilderExecutableResource(name, "dab", workingDirectory);

        return builder.AddResource(resource)
                      .WithDataApiBuilderDefaults(options)
                      .WithHttpEndpoint(port: options.Port, name: DataApiBuilderContainerResource.HttpEndpointName, isProxied: false);
    }

    /// <summary>
    /// Adds a Spring application to the application model. Executes the executable Spring app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="options">The <see cref="DataApiBuilderExecutableResourceOptions"/> to configure the DataApiBuilder api.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderExecutableResource> AddSpringApp(this IDistributedApplicationBuilder builder, string name, string workingDirectory, DataApiBuilderExecutableResourceOptions options)
    {
        return builder.AddDataApiBuilder(name, workingDirectory, options);
    }

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder,
        DataApiBuilderContainerResourceOptions options) =>
        builder.WithOtlpExporter()
               .WithEnvironment("DATAAPIBUILDER_TOOL_OPTIONS", $"-dabagent:{"options.OtelAgentPath"?.TrimEnd('/')}/opentelemetry-dabagent.jar")
               ;

    private static IResourceBuilder<DataApiBuilderExecutableResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderExecutableResource> builder,
        DataApiBuilderExecutableResourceOptions options) =>
        builder.WithOtlpExporter()
               .WithEnvironment("SERVER_PORT", options.Port.ToString(CultureInfo.InvariantCulture))
               ;
}
