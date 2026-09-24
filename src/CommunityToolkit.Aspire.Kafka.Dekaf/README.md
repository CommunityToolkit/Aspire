# CommunityToolkit.Aspire.Kafka.Dekaf library

Registers [Dekaf](https://thomhurst.github.io/Dekaf/) Kafka producers, consumers, share consumers (KIP-932), shared root clients, and admin clients with dependency injection, logging, health checks, and OpenTelemetry registration. Dekaf implements the Kafka protocol in managed .NET code and does not depend on Confluent.Kafka or librdkafka.

Also registers schema registry clients with configuration, keyed dependency injection, and health checks for use with Dekaf's JSON, Avro, and Protobuf serializers.

This is a preview integration. For a complete AppHost with Kafka, Schema Registry, and a publishing API, see the [example](../../examples/kafka-dekaf/README.md). The optional [Kafka Schema Registry hosting package](../CommunityToolkit.Aspire.Hosting.Kafka.SchemaRegistry/README.md) supplies the registry connection string through `WithReference`.

## Getting started

This integration requires .NET 10 or later and an Apache Kafka broker. Install the package in the application that sends or receives messages:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Kafka.Dekaf
```

In the application's `Program.cs`:

```csharp
using Dekaf.Consumer;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddDekafKafkaProducer<string, string>("messaging");
builder.AddDekafKafkaConsumer<string, string>("messaging", consumer => consumer
    .WithGroupId("orders-service")
    .WithAutoOffsetReset(AutoOffsetReset.Earliest)
    .SubscribeTo("orders"));
builder.AddDekafKafkaAdminClient("messaging");

using var host = builder.Build();
await host.RunAsync();
```

Dekaf's hosted initialization service initializes producers and consumers during host startup, before application hosted services registered afterward. If you resolve a client without starting the host, call its `InitializeAsync(cancellationToken)` before using it. The host owns client disposal; application services should not dispose injected clients.

Inject `Dekaf.Producer.IKafkaProducer<TKey, TValue>`, `Dekaf.Consumer.IKafkaConsumer<TKey, TValue>`, or `Dekaf.Admin.IAdminClient`:

```csharp
await producer.ProduceAsync("orders", "order-123", "created", cancellationToken);

await foreach (var message in consumer.ConsumeAsync(cancellationToken))
{
    Console.WriteLine($"{message.Key}: {message.Value}");
}
```

Provision the topic before starting a consumer that subscribes to it. Admin clients support topic creation, cluster inspection, and the other operations exposed by Dekaf.

## AppHost integration

Use the existing `Aspire.Hosting.Kafka` package in the AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var messaging = builder.AddKafka("messaging");

builder.AddProject<Projects.MyService>("my-service")
    .WithReference(messaging)
    .WaitFor(messaging);

builder.Build().Run();
```

`WithReference` supplies the `messaging` connection string to the application. The same Kafka resource works with Dekaf and Confluent clients.

## Configuration

A connection string is a comma-separated list of bootstrap servers:

```json
{
  "ConnectionStrings": {
    "messaging": "broker1:9092,broker2:9092"
  },
  "Aspire": {
    "Kafka": {
      "Dekaf": {
        "Producer": {
          "DisableHealthChecks": false,
          "Config": {
            "ClientId": "orders-producer",
            "Acks": "All",
            "LingerMs": 5
          }
        },
        "Consumer": {
          "Config": {
            "GroupId": "orders-service",
            "AutoOffsetReset": "Earliest"
          },
          "HealthCheck": {
            "Timeout": "00:00:05",
            "DegradedThreshold": 1000,
            "UnhealthyThreshold": 10000
          }
        },
        "AdminClient": {
          "HealthCheck": {
            "Timeout": "00:00:05"
          }
        }
      }
    }
  }
}
```

Settings are read from `Aspire:Kafka:Dekaf:{role}`, where the role is `Producer`, `Consumer`, `ShareConsumer`, `Client`, `AdminClient`, or `SchemaRegistry`. A subsection named after the connection overrides shared settings for both keyed and unkeyed registrations. For example, `Aspire:Kafka:Dekaf:Producer:messaging:Config:ClientId` overrides the shared producer client ID.

Configuration precedence, from lowest to highest:

1. Shared configuration section, including native `Config` options.
2. Named configuration subsection.
3. `ConnectionStrings:{connectionName}` overrides the configured connection string and native bootstrap servers.
4. `configureSettings` customizations, including `ConnectionString`.
5. `configureBuilder` customizations.

Producer, consumer, share consumer, and admin `Config` sections use [Dekaf's native configuration names](https://thomhurst.github.io/Dekaf/docs/dependency-injection), not Confluent configuration names. Native `BootstrapServers` accepts either a comma-separated string or an array. The configuration schema describes common Aspire settings; native Dekaf options remain open because the schema generator does not support their init-only properties. Root clients use `KafkaClientBuilder` callbacks for native options.

Use `configureSettings` for Aspire settings and `configureBuilder` for the full native builder surface:

```csharp
builder.AddDekafKafkaProducer<string, string>("messaging",
    configureSettings: settings => settings.DisableHealthChecks = true,
    configureBuilder: producer => producer
        .WithLinger(TimeSpan.FromMilliseconds(10))
        .WithSaslScramSha512(username, password)
        .UseTls());
```

Producer and consumer callbacks also have an `IServiceProvider` overload for serializers, deserializers, interceptors, and other application dependencies:

```csharp
builder.AddDekafKafkaProducer<string, Order>("messaging",
    configureBuilder: (services, producer) => producer
        .WithValueSerializer(services.GetRequiredService<OrderSerializer>()));
```

Admin clients bind `AdminClientOptions` from `AdminClient:Config`, including authentication, TLS, request timeouts, and transport settings. An optional `clientFactory` receives application services and the bound options:

```csharp
builder.AddDekafKafkaAdminClient("messaging",
    clientFactory: (services, options) => new Dekaf.Admin.AdminClient(
        options, services.GetRequiredService<ILoggerFactory>()));
```

Configure the admin client's security settings for the same cluster when using its connectivity check alongside producer or consumer checks. Credentials can come from a secret configuration provider. A custom factory can also construct a client through `AdminClientBuilder`; the factory is responsible for applying the supplied options. The host owns the returned client.

This integration uses Dekaf's reflection-based configuration binding. Its registration methods are annotated with `RequiresDynamicCode` and `RequiresUnreferencedCode`; trimmed and Native AOT applications are not currently supported by this integration. Dekaf itself offers separate programmatic registration APIs for those scenarios.

## Keyed registrations

Use keyed registrations for multiple clusters or independently configured clients of the same type:

```csharp
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;

builder.AddKeyedDekafKafkaProducer<string, string>("orders");
builder.AddKeyedDekafKafkaConsumer<string, string>("orders", consumer => consumer
    .WithGroupId("orders-service")
    .SubscribeTo("orders"));
builder.AddKeyedDekafKafkaAdminClient("orders");

// Resolve after starting the host.
var producer = host.Services.GetRequiredKeyedService<IKafkaProducer<string, string>>("orders");
```

The name is both the service key and connection string name. Each keyed registration receives its own health check.

## Share consumers (KIP-932)

Share consumers use Kafka share groups for queue-style processing. They expose `IKafkaShareConsumer<TKey, TValue>`, with record-level `Accept`, `Release`, and `Reject` acknowledgements. Workers can outnumber partitions, and records are not guaranteed to complete in partition order. See [Dekaf's share consumer documentation](https://thomhurst.github.io/Dekaf/docs/consumer/share-consumers).

Use Kafka 4.2 or later, enable `group.share.enable=true`, and include `share` in `group.coordinator.rebalance.protocols`. A single-broker development cluster also needs `share.coordinator.state.topic.replication.factor=1` and `share.coordinator.state.topic.min.isr=1`. The existing Aspire Kafka resource remains the broker resource; these are broker settings, not client options.

```csharp
using Dekaf.ShareConsumer;

builder.AddKeyedDekafKafkaShareConsumer<string, string>("messaging", consumer => consumer
    .WithGroupId("order-workers")
    .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
    .SubscribeTo("orders"));

// Resolve after starting the host. Use one polling loop per consumer instance.
var consumer = host.Services.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("messaging");
await foreach (var record in consumer.PollAsync(cancellationToken))
{
    await ProcessAsync(record.Value, cancellationToken);
    consumer.Acknowledge(record, AcknowledgeType.Accept);
    await consumer.CommitAsync(cancellationToken);
}
```

Use `AddDekafKafkaShareConsumer` for an unkeyed consumer. Both methods offer native builder callbacks with `IServiceProvider` access. The host initializes and disposes registered consumers. Configure native options under `ShareConsumer:Config`, including the required `GroupId` and optional `AcknowledgementMode` and `MaxPollRecords`.

Explicit mode leaves records unacknowledged until the application decides their outcome. Use `Release` for redelivery or `Reject` to discard a record. Native `PollBatchesAsync`, acquisition renewal, acknowledgement callbacks, security, and retry options remain accessible through the native interface and builder. Polling and acknowledgements on one consumer require external synchronization if used from multiple threads.

### Hosted processing and dead-letter routing

`AddDekafKafkaShareConsumerService<TService, TKey, TValue>` and its keyed counterpart register a consumer together with a `KafkaShareConsumerService<TKey, TValue>`. Each service type and key pair receives an independent consumer. Native hosted processing uses explicit acknowledgements, accepts successful records, renews acquisition locks, and handles shutdown. Keep the acknowledgement mode explicit for these services.

```csharp
builder.AddKeyedDekafKafkaShareConsumerService<OrderWorker, string, string>("messaging",
    configureSettings: settings => settings.ConfigureDeadLetterQueue = deadLetter => deadLetter
        .WithTopicSuffix(".DLQ")
        .WithMaxFailures(3)
        .AwaitDelivery(),
    configureBuilder: (services, consumer) => consumer.WithGroupId("order-workers"));

public sealed class OrderWorker(
    IKafkaShareConsumer<string, string> consumer,
    ILogger<OrderWorker> logger,
    Dekaf.Consumer.DeadLetter.DeadLetterOptions deadLetterOptions)
    : Dekaf.Extensions.Hosting.KafkaShareConsumerService<string, string>(consumer, logger, deadLetterOptions)
{
    protected override IEnumerable<string> Topics => ["orders"];

    protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
    {
        Console.WriteLine(record.Value);
        return ValueTask.CompletedTask;
    }
}
```

Forward `DeadLetterOptions` to the base constructor when enabling dead-letter routing. Bootstrap servers default to the matching consumer's servers. Configure the internal producer's authentication and TLS separately through `WithProducerConfig` for secured brokers. Native dead-letter configuration also supports retry topics. For regular consumers, `KafkaConsumerSettings.ConfigureDeadLetterQueue` registers native `DeadLetterOptions` under the consumer's service key, or `typeof(IKafkaConsumer<TKey, TValue>)` for an unkeyed consumer; pass those options to your hosted consumer. Registering a client alone does not start a processing loop or route failed records.

## Shared connections and memory budgets

`AddDekafKafkaClient` and `AddKeyedDekafKafkaClient` register a root `Dekaf.KafkaClient`. Create related producers, regular consumers, share consumers, and admin clients through that root to share infrastructure and memory budgeting:

```csharp
builder.AddKeyedDekafKafkaClient("messaging",
    configureBuilder: (services, client) => client.WithClientId("orders"));

var kafka = host.Services.GetRequiredKeyedService<Dekaf.KafkaClient>("messaging");
await using var producer = await kafka.CreateProducer<string, string>().BuildAsync(cancellationToken);
await using var consumer = await kafka.CreateShareConsumer<string, string>("order-workers")
    .SubscribeTo("orders")
    .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
    .BuildAsync(cancellationToken);
```

The host owns the root and its broker health-check client. Applications must initialize and dispose the children they create before the host shuts down. Configure shared connection security on the root builder. Standalone `AddDekafKafkaProducer` and consumer registrations remain independent; they do not automatically attach to a registered root.

## Schema registries

Register a Dekaf `ISchemaRegistryClient` for a registry implementing the Confluent Schema Registry REST API:

```csharp
using Dekaf.SchemaRegistry;
using Microsoft.Extensions.DependencyInjection;

builder.AddDekafSchemaRegistryClient("schema-registry");

const string orderSchema = """
    {"type":"object","properties":{"Id":{"type":"string"}},"required":["Id"]}
    """;

builder.AddDekafKafkaProducer<string, Order>("messaging",
    configureBuilder: (services, producer) => producer.UseJsonSchemaRegistry(
        services.GetRequiredService<ISchemaRegistryClient>(), orderSchema));

builder.AddDekafKafkaConsumer<string, Order>("messaging",
    configureBuilder: (services, consumer) => consumer
        .UseJsonSchemaRegistry(services.GetRequiredService<ISchemaRegistryClient>())
        .WithGroupId("orders-service")
        .SubscribeTo("orders"));
```

Set `ConnectionStrings:schema-registry` to a registry URL, or a comma-separated list of failover URLs. Registry settings bind from `Aspire:Kafka:Dekaf:SchemaRegistry`, followed by its named subsection. The connection string overrides `Config.Url` and `Config.Urls`; `configureSettings` runs last.

```json
{
  "ConnectionStrings": {
    "schema-registry": "https://registry.example.com"
  },
  "Aspire": {
    "Kafka": {
      "Dekaf": {
        "SchemaRegistry": {
          "HealthCheckTimeout": "00:00:05",
          "Config": {
            "RequestTimeoutMs": 5000,
            "MaxCachedSchemas": 1000,
            "LatestCacheTtlSecs": 60,
            "NormalizeSchemas": true
          }
        }
      }
    }
  }
}
```

Native `Config` also supports basic authentication, bearer tokens, OAuth, TLS, proxies, and custom headers. Supply credentials through a secret configuration provider, for example `Aspire:Kafka:Dekaf:SchemaRegistry:Config:BasicAuthUserInfo`. For programmatic configuration, assign a new `SchemaRegistryConfig` to `settings.Config`, since native options are init-only. Replacing it replaces all previously bound native options.

Use `AddKeyedDekafSchemaRegistryClient("orders-registry")` for another registry and resolve it with `services.GetRequiredKeyedService<ISchemaRegistryClient>("orders-registry")` in the serializer callbacks. Clients are singletons with shared schema caches, disposed with the host. A `clientFactory` callback receives application services and the bound native configuration for custom HTTP transports or client implementations; the service provider owns the returned client.

JSON schema serialization is included. For Avro or Protobuf, install the matching Dekaf package and use its native helper in the same producer and consumer callbacks:

| Format | Additional package | Namespace | Builder helper |
| --- | --- | --- | --- |
| JSON Schema | None | `Dekaf.SchemaRegistry` | `UseJsonSchemaRegistry` |
| Avro | `Dekaf.SchemaRegistry.Avro` | `Dekaf.SchemaRegistry.Avro` | `UseAvroSchemaRegistry` |
| Protobuf | `Dekaf.SchemaRegistry.Protobuf` | `Dekaf.SchemaRegistry.Protobuf` | `UseProtobufSchemaRegistry` |

Avro supports `GenericRecord` and generated specific records. Protobuf uses generated message types. Schema registration and subject selection are handled by Dekaf's serializers, with topic-name subjects by default. Native serializer options expose subject naming, registration behavior, and framing. JSON Schema registration does not validate payloads by default; use `Dekaf.SchemaRegistry.Json` and its validation options when validation is required. See [Dekaf schema registry documentation](https://thomhurst.github.io/Dekaf/docs/serialization/schema-registry).

## Health checks

Health checks are enabled by default and can be disabled with `DisableHealthChecks`:

| Registration | Check name | What it checks |
| --- | --- | --- |
| Producer | `Kafka.Dekaf_producer` | A producer flush checkpoint completes within the timeout. |
| Consumer | `Kafka.Dekaf_consumer` | Consumer group liveness and lag for assigned partitions. |
| Share consumer | `Kafka.Dekaf_shareconsumer` | Stable share group membership and a heartbeat within three broker-directed intervals. No records are acquired or acknowledged. |
| Shared root client | `Kafka.Dekaf_client` | An active cluster-description request through the root's shared connections. |
| Admin client | `Kafka.Dekaf_admin` | An active cluster-description request returns at least one broker. |
| Schema registry | `Kafka.Dekaf_schema_registry` | An authenticated request to list subjects succeeds, including for an empty registry. |

Keyed check names append `_{name}`. Producer, consumer, admin, and root check timeouts default to five seconds and can be configured through `HealthCheck.Timeout`. The share consumer check reads a local status snapshot; idle members with no assigned partitions can be healthy. The schema registry check defaults to 30 seconds, configurable through `HealthCheckTimeout`; it requires permission to list subjects and does not register or modify schemas.

A successful producer flush does **not** prove broker connectivity or successful message delivery. An idle producer can pass its flush check while brokers are unavailable. Register the admin client for active connectivity monitoring and observe produce results for delivery success. Unlike the Confluent integration's health check, these checks do not publish synthetic messages to a health-check topic.

The consumer check reports unhealthy before it has an assignment or live group membership. A live group member with no assigned partitions is a healthy standby by default. Consumer `HealthCheck` options also expose lag thresholds and `NoAssignmentStatus`. See [Dekaf health checks](https://thomhurst.github.io/Dekaf/docs/health-checks) for the precise guarantees.

## Observability

Dekaf writes logs through the application's `ILoggerFactory` under `Dekaf.*` categories. This integration registers the `Dekaf` activity source and meter with OpenTelemetry. Configure exporters through the application's service defaults or OpenTelemetry configuration.

Tracing includes producer spans and regular consumer spans linked to the producing trace. Metrics include sent and consumed message counts, operation duration, retries, bytes, consumer lag, and internal producer/consumer measurements. This integration forwards telemetry emitted by Dekaf; it does not synthesize per-record share consumer spans. See [Dekaf observability](https://thomhurst.github.io/Dekaf/docs/observability).

`DisableTracing` and `DisableMetrics` prevent a registration from subscribing OpenTelemetry to the corresponding source or meter. Dekaf shares one source and meter across all clients, so another enabled registration or an application listener can still collect telemetry from those clients.

Schema registry registration adds a health check but does not subscribe to Kafka's source or meter. Standard `HttpClient` instrumentation, when configured by the application, observes registry HTTP requests.

## Migrating from Aspire.Confluent.Kafka

Replace the package reference and registration methods with `AddDekafKafkaProducer` and `AddDekafKafkaConsumer`, then inject Dekaf's client interfaces. The `Dekaf` prefix lets both integrations coexist during migration. The connection string and AppHost Kafka resource can remain unchanged.

Update native configuration and serialization callbacks for Dekaf. Compression codecs and additional schema serialization formats are available through optional Dekaf packages. Transactions, partition assignment, and other native features remain available through the clients and builder callbacks. Review [Dekaf's migration guide](https://thomhurst.github.io/Dekaf/docs/migrating-from-confluent-kafka), particularly its offset-storage and delivery-semantics differences.
