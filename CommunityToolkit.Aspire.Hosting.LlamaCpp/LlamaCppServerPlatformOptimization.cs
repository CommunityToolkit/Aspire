using System;
using System.Collections.Generic;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.LlamaCpp;
public enum LlamaCppServerPlatformOptimization
{
    Default,
    Cuda,
    Cuda13,
    Rocm,
    Musa,
    Intel,
    Vulkan,
    OpenVino,
    S390x
}
