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
        var containerConsumer = builder.AddContainer("container-consumer", "node", "22-alpine")
            .WithReference(serviceBus)
            .WaitFor(azure)
            .WithArgs("-e", """
                const net = require('node:net');
                const assert = require('node:assert/strict');
                const connectionString = process.env.ConnectionStrings__servicebus;
                const endpoint = new URL(connectionString.split(';')[0].slice('Endpoint='.length));
                assert.notEqual(endpoint.hostname, 'localhost');
                const header = Buffer.from([65, 77, 81, 80, 0, 1, 0, 0]);
                const socket = net.connect({ host: endpoint.hostname, port: Number(endpoint.port) }, () => socket.write(header));
                socket.setTimeout(20000, () => { throw new Error('AMQP handshake timed out'); });
                let response = Buffer.alloc(0);
                socket.on('data', data => {
                    response = Buffer.concat([response, data]);
                    if (response.length >= header.length) {
                        assert.deepEqual(response.subarray(0, header.length), header);
                        socket.destroy();
                    }
                });
                socket.on('end', () => assert.ok(response.length >= header.length, 'Missing AMQP response'));
                """);

        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(azure.Resource.Name, cancellationToken);

        await foreach (var resourceEvent in app.ResourceNotifications.WatchAsync(cancellationToken))
        {
            if (resourceEvent.Resource == containerConsumer.Resource && resourceEvent.Snapshot.ExitCode is { } exitCode)
            {
                Assert.Equal(0, exitCode);
                break;
            }
        }

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
