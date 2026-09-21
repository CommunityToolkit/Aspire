using System.Net.Http.Json;
using CommunityToolkit.Aspire.Testing;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.SchemaRegistry;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Kafka.SchemaRegistry.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Kafka_Dekaf_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Kafka_Dekaf_AppHost>>
{
    [Fact]
    public async Task ExamplePublishesSchemaRegisteredMessage()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("api", timeout.Token);
        using var api = fixture.CreateHttpClient("api", "http");
        var message = new Message($"hello-{Guid.NewGuid():N}");
        using var response = await api.PostAsJsonAsync("/messages", message, timeout.Token);
        response.EnsureSuccessStatusCode();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["ConnectionStrings:messaging"] = await fixture.App.GetConnectionStringAsync("messaging", timeout.Token);
        builder.Configuration["ConnectionStrings:schema-registry"] = await fixture.App.GetConnectionStringAsync("schema-registry", timeout.Token);
        builder.AddDekafSchemaRegistryClient("schema-registry");
        builder.AddDekafKafkaConsumer<string, Message>("messaging", (services, consumer) => consumer
            .UseJsonSchemaRegistry(services.GetRequiredService<ISchemaRegistryClient>())
            .WithGroupId($"registry-tests-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .SubscribeTo("messages"));
        using var host = builder.Build();
        await host.StartAsync(timeout.Token);
        try
        {
            var consumer = host.Services.GetRequiredService<IKafkaConsumer<string, Message>>();
            var received = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
            Assert.NotNull(received);
            Assert.Equal("message", received.Value.Key);
            Assert.Equal(message, received.Value.Value);
            var registry = host.Services.GetRequiredService<ISchemaRegistryClient>();
            Assert.Contains("messages-value", await registry.GetAllSubjectsAsync(timeout.Token));
            var schema = await registry.GetSchemaBySubjectAsync("messages-value", cancellationToken: timeout.Token);
            Assert.Equal(SchemaType.Json, schema.Schema.SchemaType);
        }
        finally
        {
            await host.StopAsync(timeout.Token);
        }
    }
}
