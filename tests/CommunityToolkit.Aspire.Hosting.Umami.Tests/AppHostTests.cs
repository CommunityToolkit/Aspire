// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Umami.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Umami_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Umami_AppHost>>
{
    [Fact]
    public async Task UmamiResourceStartsAndRespondsOk()
    {
        const string resourceName = "umami";
        var @event = await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(HealthStatus.Healthy, @event.Snapshot.HealthStatus);
    }
}