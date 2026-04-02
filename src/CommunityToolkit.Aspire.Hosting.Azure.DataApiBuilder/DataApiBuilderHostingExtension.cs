using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding DataApiBuilder api to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DataApiBuilderHostingExtension
{
    /// <summary>
    /// Adds a DataAPIBuilder application to the application model. Executes the pre-built containerized DataAPIBuilder engine.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>
    /// <remarks>
    /// This overload is not available in polyglot app hosts. Use <see cref="AddDataAPIBuilder(IDistributedApplicationBuilder, string, string[], int?)"/> instead.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "Polyglot app hosts use the overload that makes both configFilePaths and httpPort optional.")]
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        params string[] configFilePaths)
    {
        return builder.AddDataAPIBuilder(name, null, configFilePaths);
    }

    /// <summary>
    /// Adds a DataAPIBuilder application to the application model. Executes the pre-built containerized DataAPIBuilder engine.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="httpPort">The HTTP port number for the Data API Builder container.</param>
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>
    /// <remarks>
    /// This overload is not available in polyglot app hosts. Use <see cref="AddDataAPIBuilder(IDistributedApplicationBuilder, string, string[], int?)"/> instead.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "Polyglot app hosts use the overload that makes both configFilePaths and httpPort optional to avoid optional parameter ordering issues.")]
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        params string[] configFilePaths)
    {
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));

        var resource = new DataApiBuilderContainerResource(name);

        var rb = builder.AddResource(resource)
            .WithImage(DataApiBuilderContainerImageTags.Image)
            .WithImageTag(DataApiBuilderContainerImageTags.Tag)
            .WithImageRegistry(DataApiBuilderContainerImageTags.Registry)
            .WithHttpEndpoint(port: httpPort,
                targetPort: DataApiBuilderContainerResource.HttpEndpointPort,
                name: DataApiBuilderContainerResource.HttpEndpointName)
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

    /// <summary>
    /// Adds a DataAPIBuilder application to the application model. Executes the pre-built containerized DataAPIBuilder engine.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>
    /// <param name="httpPort">The HTTP port number for the Data API Builder container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport("addDataAPIBuilder", Description = "Adds a Data API Builder container resource")]
    internal static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string[]? configFilePaths = null,
        int? httpPort = null) =>
        builder.AddDataAPIBuilder(name, httpPort, configFilePaths ?? []);

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder) =>
        builder.WithOtlpExporter()
            .WithHttpHealthCheck("/health");
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental
