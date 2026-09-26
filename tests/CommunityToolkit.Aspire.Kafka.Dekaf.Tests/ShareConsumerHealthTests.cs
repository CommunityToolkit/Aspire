// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Moq;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class ShareConsumerHealthTests
{
    [Theory]
    [InlineData(false, CoordinatorState.Stable, 1, false, HealthStatus.Healthy)]
    [InlineData(false, CoordinatorState.Stable, 10, false, HealthStatus.Unhealthy)]
    [InlineData(false, CoordinatorState.Stable, 1, true, HealthStatus.Unhealthy)]
    [InlineData(false, CoordinatorState.Unjoined, 1, false, HealthStatus.Unhealthy)]
    [InlineData(false, CoordinatorState.Joining, 1, false, HealthStatus.Unhealthy)]
    [InlineData(true, CoordinatorState.Stable, 1, false, HealthStatus.Unhealthy)]
    public async Task HealthChecksMembershipWithoutAcquiringOrAcknowledgingRecords(
        bool stopped, CoordinatorState state, int heartbeatAge, bool heartbeatFailed, HealthStatus expected)
    {
        var consumer = new Mock<IKafkaShareConsumer<string, string>>(MockBehavior.Strict);
        consumer.As<IKafkaClientStatusProvider>().Setup(client => client.GetStatus()).Returns(new KafkaClientStatus
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Role = KafkaClientRole.ShareConsumer,
            IsStopped = stopped,
            Brokers = [],
            ConsumerGroup = new ConsumerGroupStatus
            {
                HasConsumerGroup = true,
                State = state,
                MemberId = "idle-worker",
                CoordinatorId = 0,
                GenerationOrMemberEpoch = 1,
                HeartbeatInterval = TimeSpan.FromSeconds(3),
                TimeSinceLastHeartbeat = TimeSpan.FromSeconds(heartbeatAge),
                LastHeartbeatFailure = heartbeatFailed ? "Disconnected" : null,
                Assignment = []
            }
        });
        var builder = ClientTestHelpers.CreateBuilder();
        builder.AddKeyedDekafKafkaShareConsumer<string, string>("messaging");
        builder.Services.AddKeyedSingleton("messaging", consumer.Object);
        await using var services = builder.Services.BuildServiceProvider();
        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        Assert.Equal(expected, report.Status);
        consumer.As<IKafkaClientStatusProvider>().Verify(client => client.GetStatus(), Times.Once);
        consumer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ShareConsumerWithoutInitializationIsUnhealthy()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.AddDekafKafkaShareConsumer<string, string>("messaging");
        await using var services = builder.Services.BuildServiceProvider();
        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }
}
