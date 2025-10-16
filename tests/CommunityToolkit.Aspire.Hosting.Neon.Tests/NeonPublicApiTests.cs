// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Neon.Tests;

[RequiresDocker]
public class NeonPublicApiTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Neon_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Neon_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "neon";
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
