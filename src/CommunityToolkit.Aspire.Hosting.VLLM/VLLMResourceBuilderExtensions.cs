using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.VLLM;


namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding vLLM resources to the application model.
/// </summary>
public static class VLLMResourceBuilderExtensions
{
    /// <summary>
    /// Adds a vLLM OpenAI-compatible inference server container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">An optional fixed host port to bind to the vLLM container. This will be provided randomly by Aspire if not set.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// The resource health check probes vLLM's <c>/health</c> endpoint, which only succeeds once the model is loaded,
    /// so resources that wait for this resource don't start until the server can serve requests.
    /// The container is started with <c>--shm-size 8g</c> because vLLM's engine processes communicate through shared memory,
    /// and the container runtime's default <c>/dev/shm</c> size is too small for the engine to start.
    /// Add exactly one model using <see cref="AddModel(IResourceBuilder{VLLMResource}, string)"/> before starting or publishing.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<VLLMResource> AddVLLM(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resource = new VLLMResource(name);
        return builder.AddResource(resource)
            .WithAnnotation(new ContainerImageAnnotation { Image = VLLMContainerImageTags.Image, Tag = VLLMContainerImageTags.Tag, Registry = VLLMContainerImageTags.Registry })
            .WithContainerRuntimeArgs("--shm-size", "8g")
            .WithIconName("BrainCircuit")
            .WithHttpEndpoint(port: port, targetPort: VLLMResource.DefaultContainerPort, name: VLLMResource.PrimaryEndpointName)
            .WithArgs(context =>
            {
                var model = resource.Model ?? throw new DistributedApplicationException(
                    $"The vLLM resource '{resource.Name}' requires a model. Call AddModel on the resource before starting or publishing.");
                context.Args.Insert(0, model.ModelName);
            })
            .WithHttpHealthCheck("/health");
    }

    /// <summary>
    /// Adds GPU support to the vLLM container.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="vendor">The GPU vendor, defaults to <see cref="VLLMGpuVendor.Nvidia"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// For NVIDIA, this passes <c>--gpus all</c> (or <c>--device nvidia.com/gpu=all</c> when using Podman) to the container runtime.
    /// For AMD, this switches to the <c>vllm/vllm-openai-rocm</c> image and exposes <c>/dev/kfd</c> and <c>/dev/dri</c>.
    /// </remarks>
    [AspireExport("withVLLMGPUSupport", MethodName = "withGPUSupport")]
    public static IResourceBuilder<VLLMResource> WithGPUSupport(this IResourceBuilder<VLLMResource> builder, VLLMGpuVendor vendor = VLLMGpuVendor.Nvidia)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return vendor switch
        {
            VLLMGpuVendor.Nvidia => builder.WithNvidiaGPUSupport(),
            VLLMGpuVendor.AMD => builder.WithAMDGPUSupport(),
            _ => throw new ArgumentException("Invalid GPU vendor", nameof(vendor))
        };
    }

    /// <summary>
    /// Adds a data volume for the Hugging Face cache so downloaded model weights persist across container restarts.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport("withVLLMDataVolume", MethodName = "withDataVolume")]
    public static IResourceBuilder<VLLMResource> WithDataVolume(this IResourceBuilder<VLLMResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "huggingface"), "/root/.cache/huggingface", isReadOnly);
    }

    /// <summary>
    /// Sets the <c>HF_TOKEN</c> environment variable so vLLM can download gated Hugging Face models.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="token">A parameter resource that contains the Hugging Face token.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<VLLMResource> WithHuggingFaceToken(this IResourceBuilder<VLLMResource> builder, IResourceBuilder<ParameterResource> token)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(token);

        return builder.WithEnvironment("HF_TOKEN", token);
    }

    /// <summary>
    /// Adds the model that the vLLM server loads and serves.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="model">A Hugging Face model ID or a path to a model inside the container.</param>
    /// <returns>A builder for the model resource.</returns>
    /// <remarks>
    /// The resource name is generated from the server and model names.
    /// A vLLM server can serve only one base model; add another server for another model.
    /// </remarks>
    [AspireExport("addVLLMModel", MethodName = "addModel")]
    public static IResourceBuilder<VLLMModelResource> AddModel(this IResourceBuilder<VLLMResource> builder, string model)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var modelName = model.TrimEnd('/', '\\').Split('/', '\\').Last().Split(':')[0];
        var sanitizedName = string.Concat(modelName.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-')).Trim('-');
        var name = $"{builder.Resource.Name}-{(sanitizedName.Length == 0 ? "model" : sanitizedName)}";

        return builder.AddModel(name, model);
    }

    /// <summary>
    /// Adds the model that the vLLM server loads and serves, with an explicit resource name.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="name">The name of the model resource.</param>
    /// <param name="model">A Hugging Face model ID or a path to a model inside the container.</param>
    /// <returns>A builder for the model resource.</returns>
    /// <remarks>
    /// A vLLM server can serve only one base model; add another server for another model.
    /// </remarks>
    /// <exception cref="DistributedApplicationException">Thrown when the server already has a model.</exception>
    [AspireExport("addNamedVLLMModel", MethodName = "addNamedModel")]
    public static IResourceBuilder<VLLMModelResource> AddModel(this IResourceBuilder<VLLMResource> builder, [ResourceName] string name, string model)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (builder.Resource.Model is not null)
        {
            throw new DistributedApplicationException(
                $"The vLLM resource '{builder.Resource.Name}' already has a model. Add another AddVLLM resource for another model.");
        }

        var modelResource = new VLLMModelResource(name, model, builder.Resource);
        var modelBuilder = builder.ApplicationBuilder.AddResource(modelResource).WithIconName("BrainCircuit");
        builder.Resource.Model = modelResource;
        return modelBuilder;
    }

    /// <summary>
    /// Sets the model name that clients use to address the served model (<c>--served-model-name</c>).
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="servedModelName">The model name exposed through the OpenAI-compatible API.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>The served name is included in the model resource's connection string. Calling this method again replaces the previous name.</remarks>
    [AspireExport]
    public static IResourceBuilder<VLLMResource> WithServedModelName(this IResourceBuilder<VLLMResource> builder, string servedModelName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(servedModelName);

        var resource = builder.Resource;
        var isFirstName = resource.ServedModelName is null;
        resource.ServedModelName = servedModelName;

        return isFirstName
            ? builder.WithArgs(context =>
            {
                context.Args.Add("--served-model-name");
                context.Args.Add(resource.ServedModelName!);
            })
            : builder;
    }

    private static IResourceBuilder<VLLMResource> WithNvidiaGPUSupport(this IResourceBuilder<VLLMResource> builder)
    {
        return builder.ApplicationBuilder.GetContainerRuntime() switch
        {
            "podman" => builder.WithContainerRuntimeArgs("--device", "nvidia.com/gpu=all"),
            _ => builder.WithContainerRuntimeArgs("--gpus", "all"),
        };
    }

    private static IResourceBuilder<VLLMResource> WithAMDGPUSupport(this IResourceBuilder<VLLMResource> builder)
    {
        if (builder.Resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var containerAnnotation)
            && containerAnnotation.Image == VLLMContainerImageTags.Image)
        {
            containerAnnotation.Image = VLLMContainerImageTags.RocmImage;
        }

        return builder.WithContainerRuntimeArgs("--device", "/dev/kfd", "--device", "/dev/dri");
    }

    private static string? GetContainerRuntime(this IDistributedApplicationBuilder builder)
        // For config names, see https://github.com/dotnet/aspire/blob/91481d9b5a5602d3d641fa8ade554c95bcac22b5/src/Shared/KnownConfigNames.cs
        => (builder.Configuration["ASPIRE_CONTAINER_RUNTIME"]
        ?? builder.Configuration["DOTNET_ASPIRE_CONTAINER_RUNTIME"])?.ToLowerInvariant();
}
