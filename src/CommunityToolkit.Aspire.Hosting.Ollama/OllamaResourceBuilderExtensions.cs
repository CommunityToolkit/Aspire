using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Ollama;

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
    /// <param name="modelName">The name of the LLM to download on initial startup. llama3 by default. This can be set to null to not download any models.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> AddOllama(this IDistributedApplicationBuilder builder,
      string name = "Ollama", int? port = null, string modelName = "llama3")
    {
        builder.Services.TryAddLifecycleHook<OllamaResourceLifecycleHook>();
        var resource = new OllamaResource(name, modelName);
        return builder.AddResource(resource)
          .WithAnnotation(new ContainerImageAnnotation { Image = OllamaContainerImageTags.Image, Tag = OllamaContainerImageTags.Tag, Registry = OllamaContainerImageTags.Registry })
          .WithHttpEndpoint(port, 11434, OllamaResource.OllamaEndpointName)
          .WithVolume("ollama", "/root/.ollama")
          .ExcludeFromManifest()
          .PublishAsContainer();
    }
}
