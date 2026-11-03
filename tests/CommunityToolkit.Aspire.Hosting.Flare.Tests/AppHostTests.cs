// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.Flare.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Flare_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Flare_AppHost>>
{
    [Theory]
    [InlineData("flare")]
    [InlineData("flare-api")]
    [InlineData("flare-alert-worker")]
    [InlineData("flare-dashboard")]
    public async Task FlareResourcesStartAndBecomeHealthy(string resourceName)
    {
        var @event = await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(3));

        Assert.Equal(HealthStatus.Healthy, @event.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task IngestAcceptsOtlpLogs()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("flare").WaitAsync(TimeSpan.FromMinutes(3));

        using var client = fixture.CreateHttpClient("flare", "otlp-http");
        using var content = new StringContent(
            """
            {"resourceLogs":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"flare-hosting-test"}}]},"scopeLogs":[{"logRecords":[{"timeUnixNano":"1700000000000000000","severityText":"INFO","body":{"stringValue":"hello from the toolkit tests"}}]}]}]}
            """,
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/v1/logs", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
