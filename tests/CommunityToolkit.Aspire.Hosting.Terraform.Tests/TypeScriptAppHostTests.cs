// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Terraform.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostBuildsAndRuns()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Terraform.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Terraform",
            exampleName: "terraform",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
