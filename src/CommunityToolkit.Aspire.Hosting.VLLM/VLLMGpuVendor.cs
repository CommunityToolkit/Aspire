[assembly: Aspire.Hosting.AspireExport(typeof(Aspire.Hosting.VLLMGpuVendor))]

namespace Aspire.Hosting;

/// <summary>
/// Represents the GPU vendors supported by the vLLM container.
/// </summary>
public enum VLLMGpuVendor
{
    /// <summary>
    /// NVIDIA GPUs via the NVIDIA Container Toolkit.
    /// </summary>
    Nvidia,
    /// <summary>
    /// AMD GPUs via ROCm. Uses the <c>vllm/vllm-openai-rocm</c> image.
    /// </summary>
    AMD
}
