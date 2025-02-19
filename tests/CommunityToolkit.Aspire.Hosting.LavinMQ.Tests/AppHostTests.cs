using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Hosting.LavinMQ.MassTransit;
using CommunityToolkit.Aspire.Testing;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.LavinMQ.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_LavinMQ_MassTransit> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_LavinMQ_MassTransit>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        const string resourceName = "masstransitExample";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);
        
        HttpResponseMessage response = await httpClient.PostAsync("/send/Hello%20World", new StringContent(""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReplyShouldBeReceived()
    {
        const string resourceName = "masstransitExample";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);
        
        HttpResponseMessage response = await httpClient.PostAsync("/send/Hello%20World", new StringContent(""));

        string message = (await response.Content.ReadAsStringAsync());
        Assert.Equal("I've received your message: Hello World", message);
    }

    [Fact]
    public async Task WhenMessageIsSendItShouldBeReceivedByConsumer()
    {
        const string resourceName = "masstransitExample";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);
        
        MessageCounter? oldMessageCounter = await httpClient.GetFromJsonAsync<MessageCounter>("/received");
        Assert.NotNull(oldMessageCounter);
        
        await httpClient.PostAsync("/send/Hello%20World", new StringContent(""));

        MessageCounter? messageCounter = await httpClient.GetFromJsonAsync<MessageCounter>("/received");

        Assert.NotNull(messageCounter);
        Assert.True(messageCounter.ReceivedMessages > oldMessageCounter.ReceivedMessages);
    }
}