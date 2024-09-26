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
        ArgumentNullException.ThrowIfNullOrEmpty(modelName, nameof(modelName));

        builder.Resource.AddModel(modelName);
        return builder;
    }

    /// <summary>
    /// Sets the default model to be configured on the Ollama server.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="modelName">The name of the model.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> WithDefaultModel(this IResourceBuilder<OllamaResource> builder, string modelName)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNullOrEmpty(modelName, nameof(modelName));

        builder.Resource.SetDefaultModel(modelName);
        return builder;
    }
}
