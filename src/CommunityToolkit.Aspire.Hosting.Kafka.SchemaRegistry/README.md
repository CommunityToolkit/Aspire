# Kafka Schema Registry hosting integration

Use this integration to model, configure, and orchestrate a Confluent Schema Registry container backed by an existing Aspire Kafka resource. The registry is a separate service dependency that applications can reference by URL.

## Getting started

Install the preview integration in your AppHost:

```shell
aspire add CommunityToolkit.Aspire.Hosting.Kafka.SchemaRegistry
```

This integration requires .NET 10 and a container runtime. It uses `confluentinc/cp-schema-registry:8.2.0` and composes with `Aspire.Hosting.Kafka`.

## Usage example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddKafka("messaging");
var registry = builder.AddKafkaSchemaRegistry("schema-registry", kafka);

builder.AddProject<Projects.Worker>("worker")
    .WithReference(kafka)
    .WithReference(registry)
    .WaitFor(kafka)
    .WaitFor(registry);

builder.Build().Run();
```

For a TypeScript AppHost, add the integration and use the generated API:

```typescript
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const kafka = await builder.addKafka("messaging");
const registry = await builder.addKafkaSchemaRegistry("schema-registry", kafka);
const registryUri = await registry.uriExpression();

await builder.build().run();
```

The registry waits for Kafka to become healthy, connects through Kafka's container-facing internal listener, and checks `/subjects` for readiness. Host ports are allocated automatically; pass `port: 18081` to select a fixed registry host port.

The defaults use unauthenticated HTTP and plaintext Kafka with a schema-topic replication factor of one, intended for local development. The resource participates in publish output using deferred endpoint expressions. Publishing does not configure production authentication, TLS, or high availability; configure those separately before deploying outside a trusted development environment.

Schemas are stored in Kafka's `_schemas` topic. To persist them, configure `.WithDataVolume()` on the Kafka resource. The registry does not need a separate data volume. Registries backed by the same Kafka cluster share that topic by default; use different brokers or explicitly configure `SCHEMA_REGISTRY_KAFKASTORE_TOPIC` when isolation is required.

Standard container customization remains available, including `.WithImageTag(...)` and `.WithEnvironment(...)`. The helper is independent of the application's Kafka client and can be used with Dekaf or other Schema Registry-compatible clients.

## Connection Properties

`.WithReference(registry)` supplies `ConnectionStrings__schema-registry` containing the registry URL. It also exposes these structured properties for other languages:

| Property | Description | Example environment variable |
| --- | --- | --- |
| `Host` | Registry hostname resolved for the consumer's network | `SCHEMA_REGISTRY_HOST` |
| `Port` | Registry port resolved for the consumer's network | `SCHEMA_REGISTRY_PORT` |
| `Uri` | Full registry HTTP URL | `SCHEMA_REGISTRY_URI` |

For example, a registry named `registry` exposes `REGISTRY_URI`. Host processes and containers receive addresses appropriate to their network.

## Additional documentation

- [Kafka and Dekaf example](../../examples/kafka-dekaf/README.md)
- [Confluent Schema Registry documentation](https://docs.confluent.io/platform/current/schema-registry/index.html)
- [Confluent container configuration](https://docs.confluent.io/platform/current/installation/docker/config-reference.html#confluent-schema-registry-configuration)
- [Aspire integrations](https://aspire.dev/integrations/)

## Feedback & contributing

Report issues and contribute through [CommunityToolkit/Aspire](https://github.com/CommunityToolkit/Aspire).

Apache Kafka is a registered trademark of the Apache Software Foundation. Confluent is a trademark of Confluent, Inc.
