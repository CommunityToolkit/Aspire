// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using CommunityToolkit.Aspire.Testing;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class KafkaClientTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RootClientUsesOverridesAndSharesMetadata(bool keyed, bool disableDiagnostics)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["Aspire:Kafka:Dekaf:Client:ConnectionString"] = "default:9092";
        builder.Configuration["Aspire:Kafka:Dekaf:Client:messaging:ConnectionString"] = "named:9092";
        builder.Services.AddSingleton("builder:9092");
        void ConfigureSettings(KafkaClientSettings settings)
        {
            Assert.Equal("localhost:19092", settings.ConnectionString);
            settings.ConnectionString = "settings:9092";
            settings.DisableHealthChecks = disableDiagnostics;
            settings.DisableMetrics = disableDiagnostics;
            settings.DisableTracing = disableDiagnostics;
        }
        void ConfigureBuilder(IServiceProvider services, KafkaClientBuilder client)
            => client.WithBootstrapServers(services.GetRequiredService<string>());
        if (keyed)
        {
            builder.AddKeyedDekafKafkaClient("messaging", ConfigureSettings, ConfigureBuilder);
        }
        else
        {
            builder.AddDekafKafkaClient("messaging", ConfigureSettings, ConfigureBuilder);
        }
        var services = builder.Services.BuildServiceProvider();
        var root = keyed ? services.GetRequiredKeyedService<KafkaClient>("messaging") : services.GetRequiredService<KafkaClient>();
        Assert.Same(root, keyed ? services.GetRequiredKeyedService<KafkaClient>("messaging") : services.GetRequiredService<KafkaClient>());
        Assert.Equal(!disableDiagnostics, services.GetService<HealthCheckService>() is not null);
        Assert.Equal(!disableDiagnostics, services.GetService<MeterProvider>() is not null);
        Assert.Equal(!disableDiagnostics, services.GetService<TracerProvider>() is not null);
        await using (var producer = root.CreateProducer<string, string>().Build())
        await using (var consumer = root.CreateConsumer<string, string>("orders").Build())
        {
            Assert.Equal(["builder:9092"], ClientTestHelpers.GetOptions<global::Dekaf.Producer.ProducerOptions>(producer).BootstrapServers);
            // Both child roles must use the root's metadata manager, not independent clients.
            var producerMetadata = producer.GetType().GetField("_metadataManager", BindingFlags.Instance | BindingFlags.NonPublic);
            var consumerMetadata = consumer.GetType().GetField("_metadataManager", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(producerMetadata);
            Assert.NotNull(consumerMetadata);
            Assert.Same(producerMetadata.GetValue(producer), consumerMetadata.GetValue(consumer));
        }
        await services.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => root.CreateProducer<string, string>());
    }

    [Fact]
    public async Task KeyedRootClientsAreIndependent()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.AddDekafKafkaClient("messaging");
        builder.AddKeyedDekafKafkaClient("messaging");
        await using var services = builder.Services.BuildServiceProvider();
        Assert.NotSame(services.GetRequiredService<KafkaClient>(), services.GetRequiredKeyedService<KafkaClient>("messaging"));
    }
}

[Collection("Kafka Broker collection")]
public class KafkaClientFunctionalTests(KafkaContainerFixture fixture)
{
    [Theory]
    [RequiresDocker]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootClientChildrenRoundTripAndReportBrokerHealth(bool keyed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = fixture.Container!.GetBootstrapAddress();
        if (keyed)
        {
            builder.AddKeyedDekafKafkaClient("messaging");
        }
        else
        {
            builder.AddDekafKafkaClient("messaging");
        }
        using var host = builder.Build();
        var root = keyed ? host.Services.GetRequiredKeyedService<KafkaClient>("messaging") : host.Services.GetRequiredService<KafkaClient>();
        var topic = $"aspire-root-{Guid.NewGuid():N}";
        await using var admin = root.CreateAdminClient().Build();
        await admin.CreateTopicsAsync([new NewTopic { Name = topic }], cancellationToken: timeout.Token);
        try
        {
            await using var producer = await root.CreateProducer<string, string>().BuildAsync(timeout.Token);
            await using var consumer = await root.CreateConsumer<string, string>(topic)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest).SubscribeTo(topic).BuildAsync(timeout.Token);
            await producer.ProduceAsync(topic, "key", "shared-root", timeout.Token);
            var received = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
            Assert.NotNull(received);
            Assert.Equal("shared-root", received.Value.Value);
            var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(timeout.Token);
            Assert.Equal(HealthStatus.Healthy, report.Status);
            Assert.Single(report.Entries);
        }
        finally
        {
            await admin.DeleteTopicsAsync([topic], cancellationToken: timeout.Token);
        }
    }
}
