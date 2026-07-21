# CommunityToolkit.Aspire.Hosting.RedPanda

## Overview

This Aspire hosting integration runs [Redpanda](https://www.redpanda.com/) in a container. Redpanda is a Kafka API compatible streaming platform, so the resource can be referenced by any Kafka client integration (for example the official `Aspire.Confluent.Kafka` client integration). The integration exposes the Kafka API, the Schema Registry, and the Admin API, and can optionally run the Redpanda Console web UI.

## Usage

### Example 1: Add a Redpanda broker

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var redpanda = builder.AddRedPanda("redpanda");

builder.AddProject<Projects.MyApp>("myapp")
       .WithReference(redpanda)
       .WaitFor(redpanda);

builder.Build().Run();
```

The connection string injected into the referencing resource is the Kafka bootstrap server address in the form `host:port`.

### Example 2: Pin the Kafka host port

```csharp
var redpanda = builder.AddRedPanda("redpanda", port: 9092);
```

### Example 3: Tune the broker CPU and memory

```csharp
var redpanda = builder.AddRedPanda("redpanda", options =>
{
    options.CpuCount = 2;     // Redpanda --smp, defaults to 1
    options.Memory = "2G";    // Redpanda --memory, defaults to "1G"
});
```

### Example 4: Persist data with a volume or bind mount

```csharp
var redpanda = builder.AddRedPanda("redpanda")
                      .WithDataVolume();

// or

var redpanda = builder.AddRedPanda("redpanda")
                      .WithDataBindMount("./redpanda-data");
```

### Example 5: Add the Redpanda Console web UI

```csharp
var redpanda = builder.AddRedPanda("redpanda")
                      .WithConsole(console => console.WithHostPort(8080));
```

The console is configured automatically to connect to the broker's Kafka API, Schema Registry, and Admin API.

### Example 6: Add the Kafka UI web UI

```csharp
var redpanda = builder.AddRedPanda("redpanda")
                      .WithKafkaUI(kafkaUi => kafkaUi.WithHostPort(9000));
```

`WithKafkaUI` runs the same Kafka management UI (the `kafbat/kafka-ui` image) used by the official Aspire Kafka integration. It is wired automatically to the broker's Kafka API and Schema Registry, and can be used as an alternative to the Redpanda Console.

## Endpoints

| Name             | Description                                  |
| ---------------- | -------------------------------------------- |
| `kafka`          | Kafka API (host accessible)                  |
| `internal`       | Kafka API (container-to-container)           |
| `schemaregistry` | Schema Registry HTTP API                     |
| `admin`          | Admin API HTTP (used for the health check)   |

## Upstream Image

This integration pins the `redpandadata/redpanda` image (from `docker.redpanda.com`) to a specific version tag (`v26.1.10`) rather than a floating tag, and the optional console uses `redpandadata/console` pinned to `v3.7.4`. Redpanda publishes immutable, fully-versioned tags (`vYY.M.P`); update the pinned tags to adopt newer releases. The optional Kafka UI uses `kafbat/kafka-ui` (from `docker.io`) pinned to `v1.5.0`.
