using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Ollama;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

public static partial class OllamaResourceBuilderExtensions
{
    /// <summary>
    /// Adds an administration web UI Ollama to the application model using Open WebUI. This version the package defaults to the main tag of the Open WebUI container image
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
                                                        .WithEnvironment(context => ConfigureOpenWebUIContainer(context, builder.Resource))
                                                        .WaitFor(builder)
                                                        .WithHttpHealthCheck("/health")
                                                        .ExcludeFromManifest();

        configureContainer?.Invoke(resourceBuilder);

        return builder;
    }

    /// <summary>
    /// Adds a data volume to the Open WebUI container.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> for the <see cref="OpenWebUIResource"/>.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [SuppressMessage("ApiDesign", "RS0026", Justification = "The method is named WithDataVolume to be consistent with other methods.")]
    public static IResourceBuilder<OpenWebUIResource> WithDataVolume(this IResourceBuilder<OpenWebUIResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "openwebui"), "/app/backend/data", isReadOnly);
    }

    private static void ConfigureOpenWebUIContainer(EnvironmentCallbackContext context, OllamaResource resource)
    {
        context.EnvironmentVariables.Add("ENABLE_SIGNUP", "false");
        context.EnvironmentVariables.Add("ENABLE_COMMUNITY_SHARING", "false"); // by default don't enable sharing
        context.EnvironmentVariables.Add("WEBUI_AUTH", "false"); // https://docs.openwebui.com/#quick-start-with-docker--recommended
        context.EnvironmentVariables.Add("OLLAMA_BASE_URL", $"http://{resource.Name}:{resource.PrimaryEndpoint.TargetPort}");
    }
}
