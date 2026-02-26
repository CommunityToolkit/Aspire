using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Data API Builder to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DataApiBuilderHostingExtension
{
    /// <summary>
    /// Adds a Data API Builder application to the application model. Executes the pre-built containerized Data API Builder engine.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        params string[] configFilePaths)
    {
        return builder.AddDataAPIBuilder(name, null, configFilePaths);
    }

    /// <summary>
    /// Adds a Data API Builder application to the application model. Executes the pre-built containerized Data API Builder engine.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="httpPort">The HTTP port number for the Data API Builder container.</param>
    /// <param name="configFilePaths">The path to the config or schema file(s) for Data API Builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DataApiBuilderContainerResource> AddDataAPIBuilder(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        params string[] configFilePaths)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new DataApiBuilderContainerResource(name);

        var rb = builder.AddResource(resource)
            .WithIconName("Drag")
            .WithImage(DataApiBuilderContainerImageTags.Image)
            .WithImageTag(DataApiBuilderContainerImageTags.Tag)
            .WithImageRegistry(DataApiBuilderContainerImageTags.Registry)
            .WithHttpEndpoint(port: httpPort,
                targetPort: DataApiBuilderContainerResource.HttpEndpointPort,
                name: DataApiBuilderContainerResource.HttpEndpointName)
            .WithDataApiBuilderDefaults();

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
    /// Adds one or more Data API Builder configuration files to the container as read-only bind mounts.
    /// This method can be called multiple times to add additional files.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{DataApiBuilderContainerResource}"/> to configure.</param>
    /// <param name="configFiles">One or more <see cref="FileInfo"/> objects pointing to config or schema files.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configFiles"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">Thrown when a specified file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a file with the same name is already mounted.</exception>
    public static IResourceBuilder<DataApiBuilderContainerResource> WithConfigFile(
        this IResourceBuilder<DataApiBuilderContainerResource> builder,
        params FileInfo[] configFiles)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configFiles);

        foreach (var file in configFiles)
        {
            ArgumentNullException.ThrowIfNull(file);

            if (!file.Exists)
            {
                throw new FileNotFoundException($"Config file not found: {file.FullName}");
            }

            string targetPath = $"/App/{file.Name}";
            ThrowIfMountTargetExists(builder, targetPath, file.FullName);

            builder.WithBindMount(file.FullName, targetPath, isReadOnly: true);
        }

        return builder;
    }

    /// <summary>
    /// Adds all files from one or more directories to the container as individual read-only bind mounts.
    /// Each file in the directory is mounted individually (not as a folder mount). Only top-level files are included.
    /// This method can be called multiple times to add files from additional directories.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{DataApiBuilderContainerResource}"/> to configure.</param>
    /// <param name="configFolders">One or more <see cref="DirectoryInfo"/> objects pointing to directories containing config files.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configFolders"/> is <see langword="null"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a specified directory does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a file with the same name is already mounted.</exception>
    public static IResourceBuilder<DataApiBuilderContainerResource> WithConfigFolder(
        this IResourceBuilder<DataApiBuilderContainerResource> builder,
        params DirectoryInfo[] configFolders)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configFolders);

        foreach (var folder in configFolders)
        {
            ArgumentNullException.ThrowIfNull(folder);

            if (!folder.Exists)
            {
                throw new DirectoryNotFoundException($"Config directory not found: {folder.FullName}");
            }

            foreach (var file in folder.GetFiles())
            {
                string targetPath = $"/App/{file.Name}";
                ThrowIfMountTargetExists(builder, targetPath, file.FullName);

                builder.WithBindMount(file.FullName, targetPath, isReadOnly: true);
            }
        }

        return builder;
    }

    private static void ThrowIfMountTargetExists(
        IResourceBuilder<DataApiBuilderContainerResource> builder,
        string targetPath,
        string sourceDescription)
    {
        if (builder.Resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var existingMounts))
        {
            foreach (var mount in existingMounts)
            {
                if (string.Equals(mount.Target, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"A config file is already mounted to '{targetPath}'. " +
                        $"The file '{sourceDescription}' conflicts with an existing mount. " +
                        $"Each config file must have a unique filename.");
                }
            }
        }
    }

    private static IResourceBuilder<DataApiBuilderContainerResource> WithDataApiBuilderDefaults(
        this IResourceBuilder<DataApiBuilderContainerResource> builder) =>
        builder.WithOtlpExporter()
            .WithHttpHealthCheck("/health")
            .WithUrls(context =>
            {
                context.Urls.Clear();
                context.Urls.Add(new()
                {
                    Url = "/swagger",
                    DisplayText = "Swagger",
                    Endpoint = context.GetEndpoint(DataApiBuilderContainerResource.HttpEndpointName)
                });
                context.Urls.Add(new()
                {
                    Url = "/graphql",
                    DisplayText = "GraphQL",
                    Endpoint = context.GetEndpoint(DataApiBuilderContainerResource.HttpEndpointName)
                });
                context.Urls.Add(new()
                {
                    Url = "/health",
                    DisplayText = "Health",
                    Endpoint = context.GetEndpoint(DataApiBuilderContainerResource.HttpEndpointName)
                });
            });
}
