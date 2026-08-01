// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

[RequiresDocker]
public class AppHostTests
{
    private const int RepeatCount = 100;

    public static TheoryData<int> Attempts
    {
        get
        {
            TheoryData<int> attempts = [];

            for (int attempt = 1; attempt <= RepeatCount; attempt++)
            {
                attempts.Add(attempt);
            }

            return attempts;
        }
    }

    [Theory]
    [MemberData(nameof(Attempts))]
    public async Task SurrealDbAppHostRepeatsUntilFailure(int attempt)
    {
        try
        {
            await using AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SurrealDb_AppHost> fixture = new();
            await fixture.InitializeAsync();

            await AssertSurrealResourceStartsAndRespondsOk(fixture);
            await AssertApiServiceStartsAndRespondsOk(fixture);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"SurrealDb AppHost failed on attempt {attempt} of {RepeatCount}.", ex);
        }
    }

    private static async Task AssertSurrealResourceStartsAndRespondsOk(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SurrealDb_AppHost> fixture)
    {
        const string resourceName = "surreal";
        var evt = await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(KnownResourceStates.Running, evt.Snapshot.State);

        Uri tcpUri = fixture.GetEndpoint(resourceName, "tcp");
        Uri baseUri = new(tcpUri.AbsoluteUri.Replace("tcp://", "http://"));
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = false
        };
        HttpClient httpClient = new(handler)
        {
            BaseAddress = baseUri
        };

        HttpResponseMessage response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.RedirectKeepVerb, response.StatusCode);
        Assert.Equal("https://surrealdb.com/surrealist", response.Headers.Location?.AbsoluteUri);
    }

    private static async Task AssertApiServiceStartsAndRespondsOk(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SurrealDb_AppHost> fixture)
    {
        const string resourceName = "apiservice";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);

        HttpResponseMessage initResponse = await httpClient.PostAsync("/init", null);
        Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);

        HttpResponseMessage todoResponse = await httpClient.GetAsync("/api/todo");
        Assert.Equal(HttpStatusCode.OK, todoResponse.StatusCode);

        HttpResponseMessage weatherForecastResponse = await httpClient.GetAsync("/api/weatherForecast");
        Assert.Equal(HttpStatusCode.OK, weatherForecastResponse.StatusCode);

        List<object>? data = await weatherForecastResponse.Content.ReadFromJsonAsync<List<object>>();

        Assert.NotNull(data);
        Assert.NotEmpty(data);
    }
}