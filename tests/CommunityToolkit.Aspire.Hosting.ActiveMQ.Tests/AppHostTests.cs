using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;
using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_ActiveMQ_MassTransit> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_ActiveMQ_MassTransit>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        const string resourceName = "masstransitExample";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);
        
        HttpResponseMessage response = await httpClient.PostAsync("/send/Hello%20World", new StringContent(""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReplyShouldBeReceived()
    {
        const string resourceName = "masstransitExample";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);
        
        HttpResponseMessage response = await httpClient.PostAsync("/send/Hello%20World", new StringContent(""));

        (await response.Content.ReadAsStringAsync()).Should().Be("I've received your message: Hello World");
    }

    [Fact]
    public async Task WhenMessageIsSendItShouldBeReceivedByConsumer()
    {
        const string resourceName = "masstransitExample";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);
        
        MessageCounter? oldMessageCounter = await httpClient.GetFromJsonAsync<MessageCounter>("/received");
        oldMessageCounter.Should().NotBeNull();
        
        await httpClient.PostAsync("/send/Hello%20World", new StringContent(""));

        MessageCounter? messageCounter = null;
        messageCounter = await httpClient.GetFromJsonAsync<MessageCounter>("/received");

        messageCounter.Should().NotBeNull();
        messageCounter!.ReceivedMessages.Should().BeGreaterThan(oldMessageCounter!.ReceivedMessages);
    }
}