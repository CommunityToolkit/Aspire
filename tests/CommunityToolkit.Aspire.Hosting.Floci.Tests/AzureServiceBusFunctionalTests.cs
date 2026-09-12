using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using CommunityToolkit.Aspire.Testing;
using System.Net.Sockets;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

[RequiresDocker]
public class AzureServiceBusFunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServiceBusStartsAndSupportsSdkSendAndReceive(bool useBatch)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var cancellationToken = timeout.Token;

        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        string resourceNamespace = $"test-{Guid.NewGuid():N}";
        var azure = builder.AddFlociAzure("floci-az")
            .WithDockerSocket()
            .WithEnvironment("FLOCI_AZ_DOCKER_RESOURCE_NAMESPACE", resourceNamespace);
        var serviceBus = azure.WithServiceBus();
        var consumer = builder.AddExecutable("consumer", "dotnet", builder.AppHostDirectory, "--version")
            .WithReference(serviceBus)
            .WaitFor(azure);

        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(azure.Resource.Name, cancellationToken);

        Assert.NotEqual(serviceBus.Resource.AmqpEndpoint.Port, serviceBus.Resource.AmqpTlsEndpoint.Port);

        // Both listeners must exist before any management call can lazily start the namespace.
        foreach (var endpoint in new[] { serviceBus.Resource.AmqpEndpoint, serviceBus.Resource.AmqpTlsEndpoint })
        {
            using var socket = new TcpClient();
            await socket.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
        }

        var environment = await consumer.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services)
            .AsTask().WaitAsync(cancellationToken);
        string connectionString = environment["ConnectionStrings__servicebus"];
        Assert.Equal(await app.GetConnectionStringAsync(serviceBus.Resource.Name, cancellationToken), connectionString);
        var managementEndpoint = new Uri(azure.Resource.PrimaryEndpoint.Url);
        var administration = new ServiceBusAdministrationClient(
            $"Endpoint=sb://{managementEndpoint.Authority};SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");
        string queueName = $"test-{Guid.NewGuid():N}";
        await administration.CreateQueueAsync(queueName, cancellationToken);

        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender(queueName);
        string[] bodies = useBatch ? ["first", "second", "third"] : ["single"];
        if (useBatch)
        {
            using var batch = await sender.CreateMessageBatchAsync(cancellationToken);
            foreach (string body in bodies)
            {
                Assert.True(batch.TryAddMessage(new ServiceBusMessage(body)));
            }

            await sender.SendMessagesAsync(batch, cancellationToken);
        }
        else
        {
            await sender.SendMessageAsync(new ServiceBusMessage(bodies[0]), cancellationToken);
        }

        await using var receiver = client.CreateReceiver(queueName);
        foreach (string body in bodies)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(20), cancellationToken);
            Assert.NotNull(message);
            Assert.Equal(body, message.Body.ToString());
            await receiver.CompleteMessageAsync(message, cancellationToken);
        }

        await administration.DeleteQueueAsync(queueName, cancellationToken);
    }
}
