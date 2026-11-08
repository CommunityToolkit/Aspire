// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Microsoft.DotNet.XUnitExtensions;
using Xunit.v3;

namespace CommunityToolkit.Aspire.Testing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiresTestcontainersAttribute : Attribute, ITraitAttribute
{
    // Testcontainers requires a Docker-compatible API endpoint. WSLC does not provide one,
    // so Windows CI is only suitable for tests that use Aspire's container runtime.
    public static bool IsSupported => OperatingSystem.IsLinux() || !PlatformDetection.IsRunningOnCI;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
    {
        if (!IsSupported)
        {
            return [new KeyValuePair<string, string>(XunitConstants.Category, "failing")];
        }

        return [];
    }
}
