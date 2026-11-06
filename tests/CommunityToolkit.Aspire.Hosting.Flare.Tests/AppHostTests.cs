// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Data.Common;
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

    // The example's apiservice exports through WithOtlpExporter(flare). Flare's query API needs a login,
    // so the test checks the rows Flare wrote to its ClickHouse tables instead.
    [Theory]
    [InlineData("logs")]
    [InlineData("spans")]
    public async Task ExampleServiceTelemetryReachesFlare(string table)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("flare").WaitAsync(TimeSpan.FromMinutes(3), cancellationToken);

        var connectionString = new DbConnectionStringBuilder
        {
            ConnectionString = await fixture.App.GetConnectionStringAsync("clickhouse", cancellationToken)
        };

        using var client = fixture.CreateHttpClient("clickhouse", "http");
        client.DefaultRequestHeaders.Add("X-ClickHouse-User", (string)connectionString["Username"]);
        client.DefaultRequestHeaders.Add("X-ClickHouse-Key", (string)connectionString["Password"]);

        var query = Uri.EscapeDataString($"SELECT count() FROM clickhousedb.{table} WHERE ServiceName = 'apiservice'");
        var deadline = DateTime.UtcNow.AddMinutes(2);
        long count = 0;
        while (count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            using var response = await client.GetAsync($"/?query={query}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                count = long.Parse((await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
            }
        }

        Assert.True(count > 0, $"No {table} from apiservice reached Flare within two minutes.");
    }
}
