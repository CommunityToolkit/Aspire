// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Testing;
using Testcontainers.Kafka;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public sealed class KafkaContainerFixture : IAsyncLifetime
{
    public KafkaContainer? Container { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            // Testcontainers 4.14 appends a trailing comma when no extra listener is configured,
            // which Kafka 4 rejects. Keep an internal listener until that startup script is fixed.
            // https://github.com/testcontainers/testcontainers-dotnet/blob/4.14.0/src/Testcontainers.Kafka/ConfluentConfiguration.cs
            Container = new KafkaBuilder($"{TestContainerRegistry.Resolve("docker.io")}/confluentinc/cp-kafka:8.2.0")
                .WithKRaft()
                .WithListener("localhost:9095")
                .WithEnvironment("KAFKA_GROUP_SHARE_ENABLE", "true")
                .WithEnvironment("KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS", "classic,consumer,share")
                .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR", "1")
                .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR", "1")
                .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_NUM_PARTITIONS", "3")
                .Build();
            await Container.StartAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}

[CollectionDefinition("Kafka Broker collection")]
public sealed class KafkaBrokerCollection : ICollectionFixture<KafkaContainerFixture>;
