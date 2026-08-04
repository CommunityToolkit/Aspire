// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Setup()
    {
        DerivePathInfo(
            (sourceFile, projectDirectory, type, method) => new(
                directory: Path.Combine(projectDirectory, "Snapshots"),
                typeName: type.Name,
                methodName: method.Name));
    }
}
