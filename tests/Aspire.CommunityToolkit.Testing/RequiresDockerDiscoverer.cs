// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from: https://github.com/dotnet/aspire/blob/d31331d6132aeb22940dcd8834344956ba811373/tests/Aspire.Components.Common.Tests/RequiresDockerDiscoverer.cs

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Aspire.Components.Common.Tests;

public class RequiresDockerDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        if (!RequiresDockerAttribute.IsSupported)
        {
            yield return new KeyValuePair<string, string>("category", "failing");
        }
    }
}