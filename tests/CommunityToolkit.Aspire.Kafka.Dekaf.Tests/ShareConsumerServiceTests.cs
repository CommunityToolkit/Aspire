// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class ShareConsumerServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkersWithTheSameMessageTypesHaveIndependentHealthChecks(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        if (keyed)
        {
            builder.AddKeyedDekafKafkaShareConsumerService<TestShareService, string, string>("messaging");
            builder.AddKeyedDekafKafkaShareConsumerService<OtherShareService, string, string>("messaging");
            builder.AddKeyedDekafKafkaShareConsumer<string, string>("messaging");
        }
        else
        {
            builder.AddDekafKafkaShareConsumerService<TestShareService, string, string>("messaging");
            builder.AddDekafKafkaShareConsumerService<OtherShareService, string, string>("messaging");
            builder.AddDekafKafkaShareConsumer<string, string>("messaging");
        }

        await using var services = builder.Services.BuildServiceProvider();
        var workers = services.GetServices<IHostedService>().ToArray();
        var first = Assert.Single(workers.OfType<TestShareService>()).Consumer;
        var second = Assert.Single(workers.OfType<OtherShareService>()).Consumer;
        var standalone = ClientTestHelpers.GetShareConsumer(services, keyed);
        Assert.NotSame(first, second);
        Assert.NotSame(second, standalone);
        var registrations = services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Equal(3, registrations.Count);

        // Verify each check retains its worker's consumer even after the public alias is replaced.
        var checkedConsumers = registrations.Select(registration =>
        {
            var check = registration.Factory(services);
            var field = Assert.Single(check.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
                field => field.FieldType == typeof(IKafkaShareConsumer<string, string>));
            return field.GetValue(check);
        }).ToArray();
        Assert.Contains(first, checkedConsumers);
        Assert.Contains(second, checkedConsumers);
        Assert.Contains(standalone, checkedConsumers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostedServicesReceiveMatchingConsumerAndDeadLetterConfiguration(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        void Configure(KafkaShareConsumerSettings settings)
            => settings.ConfigureDeadLetterQueue = deadLetter => deadLetter.WithTopicSuffix(".failed").WithMaxFailures(3);
        if (keyed)
        {
            builder.AddKeyedDekafKafkaShareConsumerService<TestShareService, string, string>("messaging", Configure);
        }
        else
        {
            builder.AddDekafKafkaShareConsumerService<TestShareService, string, string>("messaging", Configure);
        }
        await using var services = builder.Services.BuildServiceProvider();
        var worker = Assert.Single(services.GetServices<IHostedService>().OfType<TestShareService>());
        Assert.Same(ClientTestHelpers.GetShareConsumer(services, keyed), worker.Consumer);
        Assert.Equal(ShareAcknowledgementMode.Explicit, ((IShareConsumerConfiguration)worker.Consumer).AcknowledgementMode);
        Assert.NotNull(worker.DeadLetter);
        Assert.Equal(".failed", worker.DeadLetter.TopicSuffix);
        Assert.Equal(3, worker.DeadLetter.MaxFailures);
        Assert.Equal("localhost:19092", worker.DeadLetter.BootstrapServers);
        Assert.Null(services.GetService<DeadLetterOptions>());
    }

    [Fact]
    public async Task MultipleKeyedWorkersKeepIndependentConsumersAndDeadLetterOptions()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.AddKeyedDekafKafkaShareConsumerService<TestShareService, string, string>("messaging",
            settings => settings.ConfigureDeadLetterQueue = deadLetter => deadLetter.WithTopicSuffix(".first"));
        builder.AddKeyedDekafKafkaShareConsumerService<TestShareService, string, string>("other", settings =>
        {
            settings.ConnectionString = "other:9092";
            settings.ConfigureDeadLetterQueue = deadLetter => deadLetter.WithTopicSuffix(".second");
        });
        await using var services = builder.Services.BuildServiceProvider();
        var workers = services.GetServices<IHostedService>().OfType<TestShareService>().ToArray();
        Assert.Equal(2, workers.Length);
        Assert.NotSame(workers[0].Consumer, workers[1].Consumer);
        Assert.Equal(".first", workers[0].DeadLetter!.TopicSuffix);
        Assert.Equal(".second", workers[1].DeadLetter!.TopicSuffix);
        Assert.Equal("other:9092", workers[1].DeadLetter!.BootstrapServers);
    }

    [Fact]
    public void DuplicateHostedServiceKeyFailsAtRegistration()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.AddKeyedDekafKafkaShareConsumerService<TestShareService, string, string>("messaging");
        Assert.Throws<InvalidOperationException>(() => builder.AddKeyedDekafKafkaShareConsumerService<TestShareService, string, string>("messaging"));
    }

    public sealed class TestShareService(IKafkaShareConsumer<string, string> consumer, ILogger<TestShareService> logger,
        DeadLetterOptions? deadLetterOptions = null) : KafkaShareConsumerService<string, string>(consumer, logger, deadLetterOptions)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        public DeadLetterOptions? DeadLetter { get; } = deadLetterOptions;
        protected override IEnumerable<string> Topics => ["orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    public sealed class OtherShareService(IKafkaShareConsumer<string, string> consumer, ILogger<OtherShareService> logger)
        : KafkaShareConsumerService<string, string>(consumer, logger)
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        protected override IEnumerable<string> Topics => ["other-orders"];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
