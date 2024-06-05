using Aspire.Hosting.Lifecycle;
using Raygun.Aspire.Hosting.Ollama;

namespace Aspire.Hosting.ApplicationModel
{
  public static class OllamaResourceBuilderExtensions
  {
    public static IResourceBuilder<OllamaResource> AddOllama(this IDistributedApplicationBuilder builder,
      string name = "Ollama", int? port = null, string initialModel = "llama3")
    {
      builder.Services.TryAddLifecycleHook<OllamaResourceLifecycleHook>();
      var raygun = new OllamaResource(name, initialModel);
      return builder.AddResource(raygun)
        .WithAnnotation(new ContainerImageAnnotation { Image = "ollama/ollama", Tag = "latest" })
        .WithHttpEndpoint(port, 11434, OllamaResource.OllamaEndpointName)
        .WithVolume("ollama", "/root/.ollama")
        .ExcludeFromManifest()
        .PublishAsContainer();
    }
  }
}
