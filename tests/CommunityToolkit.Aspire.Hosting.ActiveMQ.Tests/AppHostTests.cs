using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_ActiveMQ_MassTransit> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_ActiveMQ_MassTransit>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "masstransitExample";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        await Task.Delay(15000);
        var httpClient = fixture.CreateHttpClient(resourceName);
        
        var response = await httpClient.PostAsync("/send/Hello%20World", new StringContent(""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // [Fact]
    // public async Task ApiServiceCreateData()
    // {
    //     var resourceName = "apiservice";
    //
    //     await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("meilisearch").WaitAsync(TimeSpan.FromMinutes(5));
    //     await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
    //     var httpClient = fixture.CreateHttpClient(resourceName);
    //
    //     var createResponse = await httpClient.GetAsync("/create").WaitAsync(TimeSpan.FromMinutes(5));
    //     createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    //
    //     var getResponse = await httpClient.GetAsync("/get");
    //     getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    //
    //     var data = await getResponse.Content.ReadFromJsonAsync<List<object>>();
    //     Assert.NotNull(data);
    //     Assert.NotEmpty(data);
    //
    // }
}