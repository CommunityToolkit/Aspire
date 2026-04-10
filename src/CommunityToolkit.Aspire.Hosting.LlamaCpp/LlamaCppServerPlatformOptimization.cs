using System;
using System.Collections.Generic;
using System.Text;

namespace Aspire.Hosting.ApplicationModel.LlamaCpp;

/// <summary>
/// Enumerates the platform-specific optimizations supported by the llama.cpp server container image.
/// The selected optimization determines which image tag is used when creating the container.
/// </summary>
public enum LlamaCppServerPlatformOptimization
{
    /// <summary>
    /// Use the default, generic build of the server image.
    /// </summary>
    Default,

    /// <summary>
    /// Use the CUDA-accelerated build targeting CUDA (pre-1.3) environments.
    /// </summary>
    Cuda,

    /// <summary>
    /// Use the CUDA 1.3+ specific build optimized for newer CUDA toolchains.
    /// </summary>
    Cuda13,

    /// <summary>
    /// Use the ROCm-accelerated build for AMD GPUs using the ROCm stack.
    /// </summary>
    Rocm,

    /// <summary>
    /// Use the Musa-optimized build.
    /// </summary>
    Musa,

    /// <summary>
    /// Use the Intel-optimized build.
    /// </summary>
    Intel,

    /// <summary>
    /// Use the Vulkan-accelerated build.
    /// </summary>
    Vulkan,

    /// <summary>
    /// Use the OpenVINO-optimized build for Intel inference acceleration.
    /// </summary>
    OpenVino,

    /// <summary>
    /// Use the s390x architecture build.
    /// </summary>
    S390x
}
