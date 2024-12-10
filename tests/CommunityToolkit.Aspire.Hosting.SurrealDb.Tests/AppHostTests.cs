// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;
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

        var tcpUri = fixture.GetEndpoint(resourceName, "ws");
        var baseUri = new Uri(tcpUri.AbsoluteUri.Replace("tcp://", "http://"));
        var httpClient = new HttpClient();
        httpClient.BaseAddress = baseUri;

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiServiceStartsAndRespondsOk()
    {
        const string resourceName = "apiservice";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var todoResponse = await httpClient.GetAsync("/api/todo");
        todoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var initResponse = await httpClient.PostAsync("/init", null);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var weatherForecastResponse = await httpClient.GetAsync("/api/weatherForecast");
        weatherForecastResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await weatherForecastResponse.Content.ReadFromJsonAsync<List<object>>();

        data.Should().NotBeNullOrEmpty();
    }
}