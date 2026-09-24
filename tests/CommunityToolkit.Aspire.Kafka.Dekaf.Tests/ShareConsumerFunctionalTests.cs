// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Diagnostics;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

[Collection("Kafka Broker collection")]
public class ShareConsumerFunctionalTests(KafkaContainerFixture fixture)
{
    [Theory]
    [RequiresDocker]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostedShareConsumerRoutesFailedRecordsToDeadLetterTopic(bool keyed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var topic = $"aspire-share-worker-{Guid.NewGuid():N}";
        var deadLetterTopic = topic + ".DLQ";
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = fixture.Container!.GetBootstrapAddress();
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:GroupId"] = topic;
        builder.Services.AddSingleton(new Workload(topic));
        builder.AddDekafKafkaProducer<string, string>("messaging");
        builder.AddDekafKafkaAdminClient("messaging");
        builder.AddDekafKafkaConsumer<string, string>("messaging", consumer => consumer.WithGroupId(topic + "-dlq")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest).SubscribeTo(deadLetterTopic));
        void Configure(KafkaShareConsumerSettings settings)
            => settings.ConfigureDeadLetterQueue = deadLetter => deadLetter.WithMaxFailures(1).AwaitDelivery();
        if (keyed)
        {
            builder.AddKeyedDekafKafkaShareConsumerService<FailingShareService, string, string>("messaging", Configure);
        }
        else
        {
            builder.AddDekafKafkaShareConsumerService<FailingShareService, string, string>("messaging", Configure);
        }
        using var host = builder.Build();
        var admin = host.Services.GetRequiredService<IAdminClient>();
        await admin.CreateTopicsAsync([
            new NewTopic { Name = topic, NumPartitions = 1, ReplicationFactor = 1 },
            new NewTopic { Name = deadLetterTopic, NumPartitions = 1, ReplicationFactor = 1 }
        ], cancellationToken: timeout.Token);
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = topic }] = [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        }, cancellationToken: timeout.Token);
        await host.StartAsync(timeout.Token);
        try
        {
            await host.Services.GetRequiredService<IKafkaProducer<string, string>>().ProduceAsync(topic, "failed-key", "failed-value", timeout.Token);
            var consumer = host.Services.GetRequiredService<IKafkaConsumer<string, string>>();
            var received = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(45), timeout.Token);
            Assert.NotNull(received);
            Assert.Equal("failed-key", received.Value.Key);
            Assert.Equal("failed-value", received.Value.Value);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            await admin.DeleteTopicsAsync([topic, deadLetterTopic], cancellationToken: timeout.Token);
        }
    }

    [Theory]
    [RequiresDocker]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShareConsumerAcceptsReleasesAndRejectsRecords(bool keyed)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var topic = $"aspire-share-{Guid.NewGuid():N}";
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = fixture.Container!.GetBootstrapAddress();
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:GroupId"] = topic;
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:AcknowledgementMode"] = "Explicit";
        builder.AddDekafKafkaProducer<string, string>("messaging");
        builder.AddDekafKafkaAdminClient("messaging");
        if (keyed)
        {
            builder.AddKeyedDekafKafkaShareConsumer<string, string>("messaging", consumer => consumer.SubscribeTo(topic));
        }
        else
        {
            builder.AddDekafKafkaShareConsumer<string, string>("messaging", consumer => consumer.SubscribeTo(topic));
        }

        using var host = builder.Build();
        var admin = host.Services.GetRequiredService<IAdminClient>();
        await admin.CreateTopicsAsync([new NewTopic { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }], cancellationToken: timeout.Token);
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = topic }] = [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        }, cancellationToken: timeout.Token);

        await host.StartAsync(timeout.Token);
        try
        {
            var consumer = ClientTestHelpers.GetShareConsumer(host.Services, keyed);
            Assert.NotNull(((IKafkaClientStatusProvider)consumer).GetStatus().MetadataLastRefreshedAtUtc);
            var producer = host.Services.GetRequiredService<IKafkaProducer<string, string>>();
            await producer.ProduceAsync(topic, "retry", "release then accept", timeout.Token);
            await producer.ProduceAsync(topic, "reject", "reject permanently", timeout.Token);

            int? firstDeliveryCount = null;
            var accepted = false;
            var rejected = false;
            await foreach (var record in consumer.PollAsync(timeout.Token))
            {
                if (record.Key == "reject")
                {
                    Assert.False(rejected);
                    consumer.Acknowledge(record, AcknowledgeType.Reject);
                    rejected = true;
                }
                else if (firstDeliveryCount is null)
                {
                    Assert.Equal("retry", record.Key);
                    firstDeliveryCount = record.DeliveryCount;
                    consumer.Acknowledge(record, AcknowledgeType.Release);
                }
                else
                {
                    Assert.Equal("release then accept", record.Value);
                    Assert.True(record.DeliveryCount > firstDeliveryCount);
                    consumer.Acknowledge(record, AcknowledgeType.Accept);
                    accepted = true;
                }
                await consumer.CommitAsync(timeout.Token);
                if (accepted && rejected)
                {
                    break;
                }
            }

            Assert.True(accepted && rejected);
            var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(timeout.Token);
            Assert.Equal(HealthStatus.Healthy, report.Entries[keyed ? "Kafka.Dekaf_shareconsumer_messaging" : "Kafka.Dekaf_shareconsumer"].Status);

            // A new poll must not redeliver accepted or rejected records.
            using var emptyPoll = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            emptyPoll.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                await foreach (var record in consumer.PollAsync(emptyPoll.Token))
                {
                    Assert.Fail($"Unexpected redelivery of {record.Key} at offset {record.Offset}.");
                }
            }
            catch (OperationCanceledException) when (emptyPoll.IsCancellationRequested && !timeout.IsCancellationRequested)
            {
            }
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            await admin.DeleteTopicsAsync([topic], cancellationToken: timeout.Token);
        }
    }

    public sealed record Workload(string Topic);

    public sealed class FailingShareService(IKafkaShareConsumer<string, string> consumer, ILogger<FailingShareService> logger,
        Workload workload, DeadLetterOptions deadLetterOptions) : KafkaShareConsumerService<string, string>(consumer, logger, deadLetterOptions)
    {
        protected override IEnumerable<string> Topics => [workload.Topic];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Exercise dead-letter routing.");
    }
}
