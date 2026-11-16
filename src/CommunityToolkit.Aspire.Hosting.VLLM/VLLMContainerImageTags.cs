namespace CommunityToolkit.Aspire.Hosting.VLLM;

/// <summary>
/// Defines the default container registry, images, and release tag for vLLM.
/// </summary>
internal static class VLLMContainerImageTags
{
    /// <summary>
    /// The registry that hosts the vLLM images.
    /// </summary>
    public const string Registry = "docker.io";

    /// <summary>
    /// The default vLLM image for NVIDIA GPUs.
    /// </summary>
    public const string Image = "vllm/vllm-openai";

    /// <summary>
    /// The pinned vLLM release tag.
    /// </summary>
    public const string Tag = "v0.33.0";

    /// <summary>
    /// The AMD ROCm image, published separately using the same release tags.
    /// </summary>
    public const string RocmImage = "vllm/vllm-openai-rocm";
}
