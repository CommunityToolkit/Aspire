// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using Projects;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.EventStore.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<CommunityToolkit_Aspire_Hosting_EventStore_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<CommunityToolkit_Aspire_Hosting_EventStore_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "eventstore";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(1));

        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiServiceCreateAccount()
    {
        var resourceName = "apiservice";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync("eventstore")
            .WaitAsync(TimeSpan.FromMinutes(1));
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(1));

        var httpClient = fixture.CreateHttpClient(resourceName);

        var createResponse = await httpClient.PostAsJsonAsync("/account/create", new { });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var location = createResponse.Headers.Location;

        var getResponse = await httpClient.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var account = await getResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);
        Assert.Equal("John Doe", account.Name);
        Assert.Equal(100, account.Balance);
    }

    [Fact]
    public async Task ApiServiceCreateAccountAndDeposit()
    {
        var resourceName = "apiservice";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(1));

        var httpClient = fixture.CreateHttpClient(resourceName);

        var createResponse = await httpClient.PostAsJsonAsync("/account/create", new { });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var location = createResponse.Headers.Location;

        var depositResponse = await httpClient.PostAsJsonAsync($"{location!}/deposit", new { Amount = 50 });
        Assert.Equal(HttpStatusCode.OK, depositResponse.StatusCode);

        var getResponse = await httpClient.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var account = await getResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);
        Assert.Equal("John Doe", account.Name);
        Assert.Equal(150, account.Balance);
    }

    [Fact]
    public async Task ApiServiceCreateAccountAndWithdraw()
    {
        var resourceName = "apiservice";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(1));

        var httpClient = fixture.CreateHttpClient(resourceName);

        var createResponse = await httpClient.PostAsJsonAsync("/account/create", new { });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var location = createResponse.Headers.Location;

        var depositResponse = await httpClient.PostAsJsonAsync($"{location!}/withdraw", new { Amount = 90 });
        Assert.Equal(HttpStatusCode.OK, depositResponse.StatusCode);

        var getResponse = await httpClient.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var account = await getResponse.Content.ReadFromJsonAsync<AccountDto>();
        Assert.NotNull(account);
        Assert.Equal("John Doe", account.Name);
        Assert.Equal(10, account.Balance);
    }

    public record AccountDto(Guid Id, string Name, decimal Balance);
}
