// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

[RequiresKind]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Kind_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Kind_AppHost>>
{
    [Fact]
    public async Task KindClusterStartsAndBecomesHealthy()
    {
        var resourceEvent = await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync("kind-cluster")
            .WaitAsync(TimeSpan.FromMinutes(5));

        Assert.NotNull(resourceEvent);
        Assert.Equal("kind-cluster", resourceEvent.Resource.Name);
        Assert.Equal(KnownResourceStates.Running, resourceEvent.Snapshot.State?.Text);
    }
}
