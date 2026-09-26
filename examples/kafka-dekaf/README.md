# Dekaf Kafka and Schema Registry example

This example starts Kafka, Confluent Schema Registry, and an HTTP API. The AppHost supplies both connection strings and waits for Kafka and the registry to become healthy before starting the API. The API uses `CommunityToolkit.Aspire.Kafka.Dekaf` to register a producer, an admin client, and a schema registry client. JSON messages use Dekaf's schema-aware serialization.

Run from this directory with .NET 10 and Docker available:

```shell
aspire run --apphost CommunityToolkit.Aspire.Kafka.Dekaf.AppHost/CommunityToolkit.Aspire.Kafka.Dekaf.AppHost.csproj
```

Open the `api` URL shown in the dashboard. Send a request to that URL's `/messages` endpoint:

```http
POST /messages
Content-Type: application/json

{"text":"Hello from Dekaf"}
```

The API returns the Kafka partition and offset after delivery. Kafka creates the `messages` topic automatically. The registry contains the `messages-value` subject, visible through its `/subjects` endpoint. Traces and metrics are exported to the Aspire dashboard, and `/health` exposes the registered health checks.

The automated hosting test runs this example, publishes a message through the API, consumes it using a separately configured Dekaf client, and verifies schema registration against the real registry.

This example uses development-only plaintext connections. The broker and registry are ephemeral; add a Kafka data volume when persistence is needed.
