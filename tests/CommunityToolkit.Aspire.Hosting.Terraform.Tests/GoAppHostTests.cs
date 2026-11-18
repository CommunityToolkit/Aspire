// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Terraform.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Terraform.AppHost.Go",
            exampleName: "terraform",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}