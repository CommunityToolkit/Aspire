# Client integration patterns

## Configuration order

The common order is:

1. Create the settings object.
2. Bind the integration's configuration section.
3. Apply a named connection string when present.
4. Invoke the caller's configuration delegate.
5. Register services, telemetry, and health checks.

Use the actual settings type and connection-string format of the SDK. Some integrations store a raw connection string, some parse an endpoint, and others construct SDK options.

## Registration shapes

Choose the shape supported by the client:

- **Singleton:** long-lived, thread-safe SDK clients such as KurrentDB.
- **Scoped:** connection/session objects such as DuckDB connections.
- **`HttpClient`-backed:** use `IHttpClientFactory` when the SDK accepts `HttpClient` or requires HTTP handler lifetime management.
- **SDK-managed registration:** use the SDK's DI extensions when they preserve the desired lifetime and keyed behavior.
- **Fluent builder:** return a builder only when follow-up features such as chat or embedding adapters need to compose from the registered client.

Default and keyed methods should use one internal registration path where practical. A custom object service-key overload is useful only when the repository pattern or SDK use case needs it.

## Health checks

- Prefer a maintained health-check package when it accurately supports the service.
- Otherwise implement the smallest low-impact connectivity operation.
- Respect cancellation and configured timeouts.
- Use stable names and make keyed registrations distinguishable.
- Do not register a health check when required configuration is absent unless the established integration pattern intentionally defers the failure until host startup.

## Telemetry

- Register only telemetry the SDK actually emits.
- Preserve the integration's disable flags.
- Avoid duplicate instrumentation when multiple keyed clients are added.

## Examples in this repository

- `CommunityToolkit.Aspire.KurrentDB` demonstrates singleton, keyed registration, tracing, health checks, and connection-string precedence.
- `CommunityToolkit.Aspire.DuckDB.NET.Data` demonstrates scoped connection registration.
- `CommunityToolkit.Aspire.OllamaSharp` demonstrates `HttpClient` usage and a fluent builder for Microsoft.Extensions.AI adapters.
- `CommunityToolkit.Aspire.SurrealDb` demonstrates SDK-managed registration and deferred handling when options are absent.
