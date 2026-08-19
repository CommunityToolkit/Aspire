using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.StableDiffusionCpp;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding stable-diffusion.cpp resources to an
/// <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class StableDiffusionCppResourceBuilderExtensions
{
    /// <summary>
    /// Adds a stable-diffusion.cpp container resource using an official image.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="imageVariant">The official image variant to use.</param>
    /// <param name="port">An optional host port. Aspire assigns one when omitted.</param>
    /// <param name="modelsDirectory">
    /// An optional host directory used for models. Defaults to
    /// <c>.stable-diffusion-cpp/{name}/models</c> under the AppHost directory.
    /// </param>
    /// <returns>A builder for the stable-diffusion.cpp resource.</returns>
    [AspireExport]
    public static IResourceBuilder<StableDiffusionCppResource> AddStableDiffusionCpp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        StableDiffusionCppImageVariant imageVariant = StableDiffusionCppImageVariant.Cuda,
        int? port = null,
        string? modelsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        modelsDirectory ??= Path.Combine(
            builder.AppHostDirectory,
            ".stable-diffusion-cpp",
            name,
            "models");
        modelsDirectory = Path.GetFullPath(modelsDirectory, builder.AppHostDirectory);

        var outputDirectory = Path.Combine(
            builder.AppHostDirectory,
            ".stable-diffusion-cpp",
            name,
            "output");

        var resource = new StableDiffusionCppResource(
            name,
            imageVariant,
            modelsDirectory,
            outputDirectory);

        return builder.AddResource(resource)
            .WithImage(StableDiffusionCppContainerImageTags.Image)
            .WithImageSHA256(GetImageSha256(imageVariant))
            .WithImageRegistry(StableDiffusionCppContainerImageTags.Registry)
            .WithEntrypoint("/sd-server")
            .WithArgs(
                "--listen-ip", "0.0.0.0",
                "--listen-port", StableDiffusionCppResource.HttpTargetPort.ToString(),
                "--lora-model-dir", "/models/loras",
                "--hires-upscalers-dir", "/models/upscalers")
            .WithBindMount(modelsDirectory, "/models")
            .WithBindMount(outputDirectory, "/output")
            .WithHttpEndpoint(
                port: port,
                targetPort: StableDiffusionCppResource.HttpTargetPort,
                name: StableDiffusionCppResource.HttpEndpointName)
            .WithUrlForEndpoint(StableDiffusionCppResource.HttpEndpointName, annotation =>
            {
                annotation.DisplayText = "Web UI";
            })
            .WithHttpHealthCheck("/");
    }

    /// <summary>
    /// Adds the container runtime arguments required by the selected GPU backend.
    /// </summary>
    /// <param name="builder">The stable-diffusion.cpp resource builder.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<StableDiffusionCppResource> WithGPUSupport(
        this IResourceBuilder<StableDiffusionCppResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Resource.ImageVariant switch
        {
            StableDiffusionCppImageVariant.Cuda or
            StableDiffusionCppImageVariant.CudaSpark =>
                builder.ApplicationBuilder.GetContainerRuntime() == "podman"
                    ? builder.WithContainerRuntimeArgs("--device", "nvidia.com/gpu=all")
                    : builder.WithContainerRuntimeArgs("--gpus", "all"),

            StableDiffusionCppImageVariant.Vulkan or
            StableDiffusionCppImageVariant.Sycl =>
                builder.WithContainerRuntimeArgs("--device", "/dev/dri"),

            StableDiffusionCppImageVariant.Musa => builder,
            _ => throw new ArgumentOutOfRangeException(
                nameof(builder.Resource.ImageVariant),
                builder.Resource.ImageVariant,
                "Unsupported stable-diffusion.cpp image variant."),
        };
    }

    /// <summary>
    /// Processes VAE images in tiles to reduce peak memory usage during image decoding.
    /// </summary>
    /// <param name="builder">The stable-diffusion.cpp resource builder.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<StableDiffusionCppResource> WithVaeTiling(
        this IResourceBuilder<StableDiffusionCppResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithArgs("--vae-tiling");
    }

    /// <summary>
    /// Supplies a Hugging Face access token for gated or private repositories.
    /// </summary>
    /// <param name="builder">The stable-diffusion.cpp resource builder.</param>
    /// <param name="token">A secret parameter containing the Hugging Face token.</param>
    /// <returns>The resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<StableDiffusionCppResource> WithHuggingFaceToken(
        this IResourceBuilder<StableDiffusionCppResource> builder,
        IResourceBuilder<ParameterResource> token)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(token);

        builder.Resource.SetHuggingFaceToken(token.Resource);
        return builder;
    }

    /// <summary>
    /// Adds a Hugging Face model that is downloaded before the container starts.
    /// </summary>
    /// <param name="builder">The stable-diffusion.cpp resource builder.</param>
    /// <param name="repository">The Hugging Face repository identifier.</param>
    /// <param name="fileName">The model file path within the repository.</param>
    /// <param name="revision">The repository revision. Defaults to <c>main</c>.</param>
    /// <returns>A builder for the child model resource.</returns>
    [AspireExport]
    public static IResourceBuilder<StableDiffusionCppModelResource> AddHuggingFaceModel(
        this IResourceBuilder<StableDiffusionCppResource> builder,
        string repository,
        string fileName,
        string revision = "main")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var modelName = SanitizeResourceName(Path.GetFileNameWithoutExtension(fileName));

        return AddHuggingFaceModel(
            builder,
            $"{builder.Resource.Name}-{modelName}",
            repository,
            fileName,
            revision);
    }

    /// <summary>
    /// Adds a named Hugging Face model that is downloaded before the container starts.
    /// </summary>
    /// <param name="builder">The stable-diffusion.cpp resource builder.</param>
    /// <param name="name">The child model resource name.</param>
    /// <param name="repository">The Hugging Face repository identifier.</param>
    /// <param name="fileName">The model file path within the repository.</param>
    /// <param name="revision">The repository revision. Defaults to <c>main</c>.</param>
    /// <returns>A builder for the child model resource.</returns>
    [AspireExport("addNamedHuggingFaceModel")]
    public static IResourceBuilder<StableDiffusionCppModelResource> AddHuggingFaceModel(
        this IResourceBuilder<StableDiffusionCppResource> builder,
        [ResourceName] string name,
        string repository,
        string fileName,
        string revision = "main")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var model = new StableDiffusionCppModelResource(
            name,
            repository,
            fileName,
            revision,
            builder.Resource);

        builder.Resource.SetModel(model);
        builder.WithArgs("--model", $"/models/{fileName.Replace('\\', '/')}");

        builder.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(
            builder.Resource,
            (@event, cancellationToken) =>
                HuggingFaceModelDownloader.DownloadAsync(@event, model, cancellationToken));

        return builder.ApplicationBuilder.AddResource(model)
            .WithParentRelationship(builder.Resource);
    }

    private static string GetImageSha256(StableDiffusionCppImageVariant imageVariant) =>
        imageVariant switch
        {
            StableDiffusionCppImageVariant.Cuda => StableDiffusionCppContainerImageTags.CudaSha256,
            StableDiffusionCppImageVariant.CudaSpark => StableDiffusionCppContainerImageTags.CudaSparkSha256,
            StableDiffusionCppImageVariant.Vulkan => StableDiffusionCppContainerImageTags.VulkanSha256,
            StableDiffusionCppImageVariant.Sycl => StableDiffusionCppContainerImageTags.SyclSha256,
            StableDiffusionCppImageVariant.Musa => StableDiffusionCppContainerImageTags.MusaSha256,
            _ => throw new ArgumentOutOfRangeException(
                nameof(imageVariant),
                imageVariant,
                "Unsupported stable-diffusion.cpp image variant."),
        };

    private static string SanitizeResourceName(string name)
    {
        var sanitized = new string(name.Select(static character =>
            char.IsLetterOrDigit(character) || character == '-' ? character : '-').ToArray());

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return sanitized.Length == 0 ? "model" : sanitized;
    }

    private static string? GetContainerRuntime(this IDistributedApplicationBuilder builder) =>
        (builder.Configuration["ASPIRE_CONTAINER_RUNTIME"] ??
         builder.Configuration["DOTNET_ASPIRE_CONTAINER_RUNTIME"])?.ToLowerInvariant();
}
