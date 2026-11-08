// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Microsoft.DotNet.XUnitExtensions;
using Xunit.v3;

namespace CommunityToolkit.Aspire.Testing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiresDockerAttribute : Attribute, ITraitAttribute
{
    // This property is `true` when docker is *expected* to be available.
    //
    // A hard-coded *expected* value is used here to ensure that docker
    // dependent tests *fail* if docker is not available/usable in an environment
    // where it is expected to be available. A run-time check would allow tests
    // to fail silently, which is not desirable.
    //
    // scenarios:
    // - Windows: assume installed only for *local* runs as Docker is not supported on CI
    // - Linux - Local, or CI: always assume that Docker is installed
    public static bool IsSupported =>
        OperatingSystem.IsLinux() || !PlatformDetection.IsRunningOnCI; // non-Linux on CI does not support Docker

    public string? Reason { get; init; }
    public RequiresDockerAttribute(string? reason = null)
    {
        Reason = reason;
    }

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
    {
        if (!IsSupported)
        {
            return [new KeyValuePair<string, string>(XunitConstants.Category, "failing")];
        }

        return [];
    }
}