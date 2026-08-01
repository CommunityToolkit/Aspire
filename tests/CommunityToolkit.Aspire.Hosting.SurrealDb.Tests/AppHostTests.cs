// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

public class AppHostTraceSurrealFixture : AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SurrealDb_AppHost>
{
    private readonly string? previousTraceSetting = Environment.GetEnvironmentVariable("ASPIRE_SURREAL_TRACE_FOR_TESTS");

    public AppHostTraceSurrealFixture()
    {
        Environment.SetEnvironmentVariable("ASPIRE_SURREAL_TRACE_FOR_TESTS", "1");
    }

    public override async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ASPIRE_SURREAL_TRACE_FOR_TESTS", previousTraceSetting);
        await base.DisposeAsync();
    }
}

[RequiresDocker]
public class AppHostTests(AppHostTraceSurrealFixture fixture) : IClassFixture<AppHostTraceSurrealFixture>
{
    [Fact]
    public async Task SurrealResourceStartsAndRespondsOk()
    {
        const string resourceName = "surreal";
        var evt = await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(KnownResourceStates.Running, evt.Snapshot.State);

        var tcpUri = fixture.GetEndpoint(resourceName, "tcp");
        var baseUri = new Uri(tcpUri.AbsoluteUri.Replace("tcp://", "http://"));
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = baseUri
        };

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.RedirectKeepVerb, response.StatusCode);
        Assert.Equal("https://surrealdb.com/surrealist", response.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task ApiServiceStartsAndRespondsOk()
    {
        const string resourceName = "apiservice";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var initResponse = await httpClient.PostAsync("/init", null);
        Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        
        var todoResponse = await httpClient.GetAsync("/api/todo");
        Assert.Equal(HttpStatusCode.OK, todoResponse.StatusCode);

        var weatherForecastResponse = await httpClient.GetAsync("/api/weatherForecast");
        Assert.Equal(HttpStatusCode.OK, weatherForecastResponse.StatusCode);

        var data = await weatherForecastResponse.Content.ReadFromJsonAsync<List<object>>();

        Assert.NotNull(data);
        Assert.NotEmpty(data);
    }
}