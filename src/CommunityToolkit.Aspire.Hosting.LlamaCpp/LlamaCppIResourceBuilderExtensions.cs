using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.LlamaCpp;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for registering and configuring <see cref="LlamaCppServerResource"/>
/// instances on an <see cref="IDistributedApplicationBuilder"/> and for configuring
/// `LlamaCppServerResource` instances via an `IResourceBuilder{LlamaCppServerResource}`.
/// </summary>
public static class LlamaCppIResourceBuilderExtensions
{
    private const string ReasoningArgument = "LLAMA_ARG_REASONING";
    private const string ContextSizeArgument = "LLAMA_ARG_CTX_SIZE";
    private const string MultimodalProjectionUrlArgument = "LLAMA_ARG_MMPROJ_URL";
    private const string MultimodalProjectionFileArgument = "LLAMA_ARG_MMPROJ";
    private const string ModelArgument = "LLAMA_ARG_MODEL";
    private const string ModelAliasArgument = "LLAMA_ARG_ALIAS";
    private const string ModelUrlArgument = "LLAMA_ARG_MODEL_URL";
    private const string ApiKeyArgument = "LLAMA_API_KEY";


    /// <summary>
    /// Adds a new <see cref="LlamaCppServerResource"/> to the distributed application builder.
    /// </summary>
    /// <param name="builder">The distributed application builder used to register resources.</param>
    /// <param name="name">The logical name for the resource. Use the <see cref="ResourceNameAttribute"/> for validation.</param>
    /// <param name="modelUrl">A URL to the model file to be used by the server.</param>
    /// <param name="port">Optional external port to bind the HTTP endpoint to. When null, a dynamic port is assigned.</param>
    /// <param name="optimization">Platform optimization to select the appropriate container image tag.</param>
    /// <returns>An <see cref="IResourceBuilder{LlamaCppServerResource}"/> allowing further configuration of the resource.</returns>
    public static IResourceBuilder<LlamaCppServerResource> AddLlamaServer(this IDistributedApplicationBuilder builder,
            [ResourceName] string name, string modelUrl, int? port = null,
            LlamaCppServerPlatformOptimization optimization = LlamaCppServerPlatformOptimization.Default)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(modelUrl, nameof(modelUrl));

        var resource = new LlamaCppServerResource(name);

        resource.ModelName = Path.GetFileName(modelUrl);
        return builder.AddResource(resource)
              .WithAnnotation(new ContainerImageAnnotation
              {
                  Image = LlamaCppServerContainerImageTags.Image,
                  Tag = LlamaCppServerContainerImageTags.GetTag(optimization),
                  Registry = LlamaCppServerContainerImageTags.Registry
              })
              .WithEnvironment(ModelUrlArgument, modelUrl)
              .WithHttpEndpoint(port, targetPort: 8080, LlamaCppServerResource.LlamaServerEndpointName)
              .WithHttpHealthCheck("/health");
    }
    /// <summary>
    /// Adds an environment variable to the llama server resource and records the variable name on the resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">Environment variable name.</param>
    /// <param name="value">Environment variable value.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithEnvironment(this IResourceBuilder<LlamaCppServerResource> builder, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        builder.Resource.EnvironmentArgs.Add(name);
        ((IResourceBuilder<ContainerResource>)builder).WithEnvironment(name, value);
        return builder;
    }
    /// <summary>
    /// Enables or disables the llama "reasoning" feature by setting a predefined environment variable.
    /// This can only be set once per resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="useReasoning">True to enable reasoning; false to disable.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithReasoning(this IResourceBuilder<LlamaCppServerResource> builder, bool useReasoning = true)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        if (builder.Resource.EnvironmentArgs.Contains(ReasoningArgument))
        {
            throw new InvalidOperationException("Reasoning was already defined and cannot be set again. Make sure that WithReasoning is called at most once.");
        }
        return builder.WithEnvironment(ReasoningArgument, useReasoning ? "on" : "off");
    }
    /// <summary>
    /// Configures API keys that the llama server will accept. Keys are supplied as a comma separated
    /// list in a single environment variable. This can only be configured once per resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="keys">One or more keys to register with the server.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithApikeys(this IResourceBuilder<LlamaCppServerResource> builder, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(keys, nameof(keys));

        if (builder.Resource.EnvironmentArgs.Contains(ApiKeyArgument))
        {
            throw new InvalidOperationException("The instance already defined Api keys. Make sure that WithApikeys is called at most once.");
        }
        var values = string.Join(",", keys);
        return builder.WithEnvironment(ApiKeyArgument, values);
    }
    /// <summary>
    /// Sets the model context size used by the llama server via an environment variable.
    /// Can only be set once per resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="size">The context size value to set.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithContextSize(this IResourceBuilder<LlamaCppServerResource> builder, int size = 0)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        if (builder.Resource.EnvironmentArgs.Contains(ContextSizeArgument))
        {
            throw new InvalidOperationException("Context size was already defined and cannot be set again. Make sure that WithContextSize is called at most once.");
        }
        return builder.WithEnvironment(ContextSizeArgument, size.ToString());
    }
    /// <summary>
    /// Sets a human-friendly alias for the model exposed by this resource. The alias is passed
    /// to the server via an environment variable and can only be set once.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="alias">The alias to assign to the model.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithModelAlias(this IResourceBuilder<LlamaCppServerResource> builder, string alias)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
       
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new InvalidOperationException("Alias cannot be empty. Make sure that WithModelAlias is called with a valid alias.");
        }
        if (builder.Resource.EnvironmentArgs.Contains(ModelAliasArgument))
        {
            throw new InvalidOperationException("Model alias was already defined and cannot be set again. Make sure that WithModelAlias is called at most once.");
        }
        return builder.WithEnvironment(ModelAliasArgument, alias);
    }
    /// <summary>
    /// Configures a multimodal projection file for the server. The projection file URL is stored
    /// in an environment variable and the file path inside the container is also configured.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="projectionFileUrl">URL to the projection file to download into the container.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithMultimodalProjection(this IResourceBuilder<LlamaCppServerResource> builder, string projectionFileUrl)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(projectionFileUrl, nameof(projectionFileUrl));

        if (builder.Resource.EnvironmentArgs.Contains(MultimodalProjectionFileArgument))
        {
            throw new InvalidOperationException("Projection file url was already defined and cannot be set again. Make sure that WithMultimodalProjection is called at most once.");
        }
        return builder.WithEnvironment(MultimodalProjectionUrlArgument, projectionFileUrl)
                      .WithEnvironment(MultimodalProjectionFileArgument,
                              $"/models/mmproj/{Path.GetFileName(projectionFileUrl)}");
    }

    /// <summary>
    /// Attaches or creates a data volume mounted at <c>/models</c> inside the container and
    /// configures the server to use the model file stored in that volume. Only one data volume
    /// may be associated with a resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">Optional volume name. When null a generated name will be used.</param>
    /// <param name="isReadOnly">True to mount the volume as read-only inside the container.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithDataVolume(this IResourceBuilder<LlamaCppServerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        if (!string.IsNullOrWhiteSpace(builder.Resource.VolumeName))
        {
            throw new InvalidOperationException("The LlamaServer resource already has a data volume associated. It can not have more than one.");
        }
        var volumeName = name ?? VolumeNameGenerator.Generate(builder, builder.Resource.Name);
        builder.Resource.VolumeName = volumeName;
        return builder.WithVolume(volumeName, "/models", isReadOnly)
            .WithEnvironment(ModelArgument, $"/models/{builder.Resource.ModelName}");
    }
    /// <summary>
    /// Shares an existing data volume from another llama server resource. If both resources reference
    /// the same model file, the current builder will wait for the volume owner to be ready before proceeding.
    /// </summary>
    /// <param name="builder">The resource builder that will use the existing volume.</param>
    /// <param name="volumeOwner">The resource builder that owns the volume to share.</param>
    /// <param name="isReadOnly">True to mount the shared volume as read-only.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<LlamaCppServerResource> WithDataVolume(this IResourceBuilder<LlamaCppServerResource> builder, IResourceBuilder<LlamaCppServerResource> volumeOwner, bool isReadOnly = false)
    {
        if (builder == volumeOwner)
        {
            throw new InvalidOperationException($"The {nameof(volumeOwner)} object must be different than the current builder.");
        }
        var name = volumeOwner.Resource.VolumeName;

        if (volumeOwner.Resource.ModelName == builder.Resource.ModelName)
        {
            builder.WaitFor(volumeOwner);
        }

        return builder.WithDataVolume(name, isReadOnly);
    }
}
