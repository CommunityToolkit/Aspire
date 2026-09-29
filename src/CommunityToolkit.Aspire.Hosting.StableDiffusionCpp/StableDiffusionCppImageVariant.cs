namespace Aspire.Hosting;

/// <summary>
/// Official stable-diffusion.cpp container image variants.
/// </summary>
public enum StableDiffusionCppImageVariant
{
    /// <summary>NVIDIA CUDA backend.</summary>
    Cuda,

    /// <summary>NVIDIA CUDA backend optimized for DGX Spark.</summary>
    CudaSpark,

    /// <summary>Vulkan backend.</summary>
    Vulkan,

    /// <summary>Intel SYCL backend.</summary>
    Sycl,

    /// <summary>Moore Threads MUSA backend.</summary>
    Musa,
}
