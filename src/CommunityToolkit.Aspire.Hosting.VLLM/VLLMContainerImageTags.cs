namespace CommunityToolkit.Aspire.Hosting.VLLM;

internal static class VLLMContainerImageTags
{
    public const string Registry = "docker.io";
    public const string Image = "vllm/vllm-openai";
    public const string Tag = "v0.33.0";

    // AMD ROCm builds are published to a separate repository using the same release tags.
    public const string RocmImage = "vllm/vllm-openai-rocm";
}
