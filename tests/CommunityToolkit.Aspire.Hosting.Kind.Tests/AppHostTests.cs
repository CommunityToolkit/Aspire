// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

[RequiresKind]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Kind_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Kind_AppHost>>
{
    [Fact]
    public async Task KindClusterStartsAndBecomesHealthy()
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync("kind-cluster")
            .WaitAsync(TimeSpan.FromMinutes(5));
    }
}
