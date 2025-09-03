using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder;
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
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>"
    /// <remarks>
    /// At this time, this .NET Aspire DAB integration only supports HTTPS ports. 
    /// You can <see href="https://learn.microsoft.com/en-us/aspnet/core/security/docker-https?view=aspnetcore-8.0#running-pre-built-container-images-with-https">deploy DAB with HTTPS and custom certs</see> in production.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{DataApiBuilderContainerResource}"/>.</returns>
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
    /// <param name="httpPort">The HTTP port number for the Data API Builder container.</param>"
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>"
    /// <remarks>
    /// At this time, this .NET Aspire DAB integration only supports HTTPS ports. 
    /// You can <see href="https://learn.microsoft.com/en-us/aspnet/core/security/docker-https?view=aspnetcore-8.0#running-pre-built-container-images-with-https">deploy DAB with HTTPS and custom certs</see> in production.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{DataApiBuilderContainerResource}"/>.</returns>
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

        // Store the config file paths on the resource so RunAsExecutable can access them.
        resource.HttpPort = httpPort;
        resource.ConfigFilePaths = configFilePaths;

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
    /// Configure the resource builder to run the Data API Builder as an executable in Development mode.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{DataApiBuilderContainerResource}"/> to run as executable.</param>
    /// <param name="configure">The <see cref="IResourceBuilder{DataApiBuilderExecutableResource}"/> to run as executable.</param>
    /// <remarks>
    /// If publish mode is selected, this will be ignored.
    /// DAB cli is required to use this option.
    /// You can look at the documentation to <see href="https://learn.microsoft.com/en-us/azure/data-api-builder/how-to/install-cli">install dab cli</see>.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{DataApiBuilderExecutableResource}"/>.</returns>
    public static IResourceBuilder<IDataApiBuilderResource> RunAsExecutable(this IResourceBuilder<DataApiBuilderContainerResource> builder,
        Action<IResourceBuilder<DataApiBuilderExecutableResource>>? configure = null)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        // I'm using the same strategy used for PublishAsDockerfile in Aspire
        builder.ApplicationBuilder.Resources.Remove(builder.Resource);

        // Configure the resource builder to run the Data API Builder as an executable
        var resource = new DataApiBuilderExecutableResource(builder.Resource.Name);

        // We build the list of arguments to pass to the dab command
        // The final command should be similar to dab start --config file1.json file2.json
        var allArgs = new List<string> { "start" };

        // The config file paths should have been set by AddDataAPIBuilder. Enforce invocation order.
        var configFilePaths = builder.Resource.ConfigFilePaths;
        if (configFilePaths is null || configFilePaths.Length == 0)
        {
            throw new InvalidOperationException("RunAsExecutable must be called after AddDataAPIBuilder so the config file paths are available.");
        }

        allArgs.Add("--config");

        foreach (var configFilePath in configFilePaths)
        {
            var configFileName = File.Exists(configFilePath)
                ? Path.GetFileName(configFilePath)
                : throw new FileNotFoundException($"Config file not found: {configFilePath}");

            allArgs.Add(configFileName);
        }

        var rb = builder.ApplicationBuilder.AddResource(resource)
            .WithHttpEndpoint(port: builder.Resource.HttpPort,
                targetPort: DataApiBuilderContainerResource.HttpEndpointPort,
                name: DataApiBuilderContainerResource.HttpEndpointName)
            .WithHttpsEndpoint(targetPort: DataApiBuilderContainerResource.HttpsEndpointPort,
                name: DataApiBuilderContainerResource.HttpsEndpointName)
            .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5000;https://localhost:5001")
            .WithDataApiBuilderDefaults()
            .WithArgs([.. allArgs]);

        configure?.Invoke(rb);

        return rb;
    }

    private static IResourceBuilder<T> WithDataApiBuilderDefaults<T>(
        this IResourceBuilder<T> builder) where T : IDataApiBuilderResource =>
        builder.WithOtlpExporter();
}
