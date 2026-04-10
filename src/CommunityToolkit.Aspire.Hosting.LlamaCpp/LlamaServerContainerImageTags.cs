using System;
using System.Collections.Generic;
using System.Text;

namespace Aspire.Hosting.ApplicationModel.LlamaCpp;

internal class LlamaCppServerContainerImageTags
{
    // Tag constants for the different specialized builds of the server image.
    const string ServerDefaultTag = "server";
    const string ServerCudaTag = "server-cuda";
    const string ServerCuda13Tag = "server-cuda13";
    const string ServerRocmTag = " server-rocm";
    const string ServerMusaTag = "server-musa";
    const string ServerIntelTag = "server-intel";
    const string ServerVulkanTag = " server-vulkan";
    const string ServerOpenVinoTag = " server-openvino";
    const string ServerS390xTag = "server-s390x";

    /// <summary>
    /// Registry hosting the container image.
    /// </summary>
    public const string Registry = "ghcr.io";

    /// <summary>
    /// Image name for the llama.cpp server.
    /// </summary>
    public const string Image = "ggml-org/llama.cpp";

    /// <summary>
    /// Returns the image tag corresponding to the requested <see cref="LlamaCppServerPlatformOptimization"/>.
    /// </summary>
    /// <param name="opt">The optimization flavor to select.</param>
    /// <returns>A tag string to append to the image name.</returns>
    public static string GetTag(LlamaCppServerPlatformOptimization opt)
    {
        return opt switch
        {
            LlamaCppServerPlatformOptimization.Cuda => ServerCudaTag,
            LlamaCppServerPlatformOptimization.Cuda13 => ServerCuda13Tag,
            LlamaCppServerPlatformOptimization.Rocm => ServerRocmTag,
            LlamaCppServerPlatformOptimization.Musa => ServerMusaTag,
            LlamaCppServerPlatformOptimization.Intel => ServerIntelTag,
            LlamaCppServerPlatformOptimization.Vulkan => ServerVulkanTag,
            LlamaCppServerPlatformOptimization.OpenVino => ServerOpenVinoTag,
            LlamaCppServerPlatformOptimization.S390x => ServerS390xTag,
            _ => ServerDefaultTag
        };
    }
}

