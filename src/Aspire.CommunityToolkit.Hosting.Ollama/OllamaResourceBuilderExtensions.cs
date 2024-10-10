using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Utils;
using Aspire.CommunityToolkit.Hosting.Ollama;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding an Ollama container to the application model.
/// </summary>
public static class OllamaResourceBuilderExtensions
{
    private const string ConnectionStringEnvironmentName = "ConnectionStrings__";

    /// <summary>
    /// Adds the Ollama container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">An optional fixed port to bind to the Ollama container. This will be provided randomly by Aspire if not set.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> AddOllama(this IDistributedApplicationBuilder builder, string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        builder.Services.TryAddLifecycleHook<OllamaResourceLifecycleHook>();

        var resource = new OllamaResource(name);
        return builder.AddResource(resource)
          .WithAnnotation(new ContainerImageAnnotation { Image = OllamaContainerImageTags.Image, Tag = OllamaContainerImageTags.Tag, Registry = OllamaContainerImageTags.Registry })
          .WithHttpEndpoint(port: port, targetPort: 11434, name: OllamaResource.OllamaEndpointName)
          .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds the Ollama container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">An optional fixed port to bind to the Ollama container. This will be provided randomly by Aspire if not set.</param>
    /// <param name="modelName">The name of the LLM to download on initial startup. llama3 by default. This can be set to null to not download any models.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This is to maintain compatibility with the Raygun.Aspire.Hosting.Ollama package and will be removed in the next major release.</remarks>
    [Obsolete("Use AddOllama without a model name, and then the AddModel extension method to add models.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "<Pending>")]
    public static IResourceBuilder<OllamaResource> AddOllama(this IDistributedApplicationBuilder builder,
      string name = "Ollama", int? port = null, string modelName = "llama3")
    {
        return builder.AddOllama(name, port)
          .AddModel(modelName);
    }

    /// <summary>
    /// Adds a data volume to the Ollama container.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> WithDataVolume(this IResourceBuilder<OllamaResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

#pragma warning disable CTASPIRE001
        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "ollama"), "/root/.ollama", isReadOnly);
#pragma warning restore CTASPIRE001
    }

    /// <summary>
    /// Adds a model to the Ollama container.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="modelName">The name of the LLM to download on initial startup.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> AddModel(this IResourceBuilder<OllamaResource> builder, string modelName)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName, nameof(modelName));

        builder.Resource.AddModel(modelName);
        return builder;
    }

    /// <summary>
    /// Sets the default model to be configured on the Ollama server.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="modelName">The name of the model.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> WithDefaultModel(this IResourceBuilder<OllamaResource> builder, string modelName)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName, nameof(modelName));

        builder.Resource.SetDefaultModel(modelName);
        return builder;
    }

    /// <summary>
    /// Adds a reference to an Ollama resource to the application model.
    /// </summary>
    /// <typeparam name="T">The type of the resource to add Ollama to.</typeparam>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="ollama">The Ollama resource to add as a reference.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// This method adds the connection string and model references to the application model.
    /// </remarks>
    public static IResourceBuilder<T> WithReference<T>(this IResourceBuilder<T> builder, IResourceBuilder<OllamaResource> ollama) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(ollama, nameof(ollama));

        var resource = (IResourceWithConnectionString)ollama.Resource;
        return builder.WithEnvironment(context =>
        {
            var connectionStringName = resource.ConnectionStringEnvironmentVariable ?? $"{ConnectionStringEnvironmentName}{resource.Name}";

            context.EnvironmentVariables[connectionStringName] = new ConnectionStringReference(resource, optional: false);

            for (int i = 0; i < ollama.Resource.Models.Count; i++)
            {
                var model = ollama.Resource.Models[i];
                context.EnvironmentVariables[$"Aspire__OllamaSharp__{resource.Name}__Models__{i}"] = model;

                if (model == ollama.Resource.DefaultModel)
                {
                    context.EnvironmentVariables[$"Aspire__OllamaSharp__{resource.Name}__SelectedModel"] = model;
                }
            }
        });
    }

    /// <summary>
    /// Adds an administration web UI Ollama to the application model using Attu. This version the package defaults to the main tag of the Open WebUI container image
    /// </summary>
    /// <example>
    /// Use in application host with an Ollama resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var ollama = builder.AddOllama("ollama")
    ///   .WithOpenWebUI();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(ollama);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// <param name="builder">The Ollama resource builder.</param>
    /// <param name="configureContainer">Configuration callback for Open WebUI container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>See https://openwebui.com for more information about Open WebUI</remarks>
    public static IResourceBuilder<T> WithOpenWebUI<T>(this IResourceBuilder<T> builder, Action<IResourceBuilder<OpenWebUIResource>>? configureContainer = null, string? containerName = null) where T : OllamaResource
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        if (builder.ApplicationBuilder.Resources.OfType<OpenWebUIResource>().SingleOrDefault() is { } existingOpenWebUIResource)
        {
            var builderForExistingResource = builder.ApplicationBuilder.CreateResourceBuilder(existingOpenWebUIResource);
            configureContainer?.Invoke(builderForExistingResource);
            return builder;
        }

        containerName ??= $"{builder.Resource.Name}-openwebui";

        var openWebUI = new OpenWebUIResource(containerName);
        var resourceBuilder = builder.ApplicationBuilder.AddResource(openWebUI)
                                                        .WithImage(OllamaContainerImageTags.OpenWebUIImage, OllamaContainerImageTags.OpenWebUITag)
                                                        .WithImageRegistry(OllamaContainerImageTags.OpenWebUIRegistry)
                                                        .WithHttpEndpoint(targetPort: 8080, name: "http")
                                                        .WithVolume("open-webui", "/app/backend/data")
                                                        .WithEnvironment(context => ConfigureOpenWebUIContainer(context, builder.Resource))
                                                        .ExcludeFromManifest();

        configureContainer?.Invoke(resourceBuilder);

        return builder;
    }

    private static void ConfigureOpenWebUIContainer(EnvironmentCallbackContext context, OllamaResource resource)
    {
        context.EnvironmentVariables.Add("ENABLE_SIGNUP", "false");
        context.EnvironmentVariables.Add("ENABLE_COMMUNITY_SHARING", "false"); // by default don't enable sharing
        context.EnvironmentVariables.Add("WEBUI_AUTH", "false"); // https://docs.openwebui.com/#quick-start-with-docker--recommended
        context.EnvironmentVariables.Add("OLLAMA_BASE_URL", $"{resource.PrimaryEndpoint.Scheme}://{resource.PrimaryEndpoint.ContainerHost}:{resource.PrimaryEndpoint.Port}");
    }
}
