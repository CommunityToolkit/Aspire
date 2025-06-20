// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SurrealDb_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SurrealDb_AppHost>>
{
    [Fact]
    public async Task SurrealResourceStartsAndRespondsOk()
    {
        const string resourceName = "surreal";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));

        var tcpUri = fixture.GetEndpoint(resourceName, "tcp");
        var baseUri = new Uri(tcpUri.AbsoluteUri.Replace("tcp://", "http://"));
        var httpClient = new HttpClient();
        httpClient.BaseAddress = baseUri;

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiServiceStartsAndRespondsOk()
    {
        const string resourceName = "apiservice";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var todoResponse = await httpClient.GetAsync("/api/todo");
        Assert.Equal(HttpStatusCode.OK, todoResponse.StatusCode);
        
        var initResponse = await httpClient.PostAsync("/init", null);
        Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);

        var weatherForecastResponse = await httpClient.GetAsync("/api/weatherForecast");
        Assert.Equal(HttpStatusCode.OK, weatherForecastResponse.StatusCode);

        var data = await weatherForecastResponse.Content.ReadFromJsonAsync<List<object>>();

        Assert.NotNull(data);
        Assert.NotEmpty(data);
    }
}