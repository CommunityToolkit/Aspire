// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class ShareConsumerServiceTests
{
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
}
