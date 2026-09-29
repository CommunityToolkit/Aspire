using System.Text;
using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using MQTTnet;

namespace CommunityToolkit.Aspire.Hosting.Mosquitto.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Mosquitto_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Mosquitto_AppHost>>
{
    private const string ResourceName = "mqtt";

    [Fact]
    public async Task ResourceStartsAndRoundTripsMessages()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(ResourceName).WaitAsync(TimeSpan.FromMinutes(2), cts.Token);

        DistributedApplicationModel appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());

        string? connectionString = await resource.ConnectionStringExpression.GetValueAsync(cts.Token);
        Assert.NotNull(connectionString);
        Assert.StartsWith("mqtt://", connectionString);

        Uri uri = new(connectionString);
        string host = uri.Host;
        int port = uri.Port;

        MqttClientFactory mqttFactory = new();
        using IMqttClient mqttClient = mqttFactory.CreateMqttClient();

        MqttClientConnectResult connectResult = await mqttClient.ConnectAsync(
            new MqttClientOptionsBuilder().WithTcpServer(host, port).Build(), cts.Token);
        Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);

        string topic = "test/topic";
        string payload = "hello-mosquitto";
        TaskCompletionSource<string> receivedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        mqttClient.ApplicationMessageReceivedAsync += args =>
        {
            receivedTcs.TrySetResult(Encoding.UTF8.GetString(args.ApplicationMessage.Payload));
            return Task.CompletedTask;
        };

        MqttClientSubscribeOptions subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic)
            .Build();
        await mqttClient.SubscribeAsync(subscribeOptions, cts.Token);

        MqttApplicationMessage message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();
        await mqttClient.PublishAsync(message, cts.Token);

        string received = await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30), cts.Token);
        Assert.Equal(payload, received);

        await mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), cts.Token);
    }
}
