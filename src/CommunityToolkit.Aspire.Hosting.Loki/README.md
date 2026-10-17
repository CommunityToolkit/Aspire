# Loki hosting integration

Use this integration to model, configure, and orchestrate [Grafana Loki](https://grafana.com/oss/loki/) containers in an Aspire AppHost.

## Getting started

Install the integration in your AppHost:

```bash
aspire add CommunityToolkit.Aspire.Hosting.Loki
```

## Usage example

### Example 1: Add Loki and reference it from an API

In this approach, the API receives Loki's HTTP connection properties and waits for Loki to become healthy.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki");

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Loki_Api>("api")
    .WithReference(loki)
    .WaitFor(loki);

builder.Build().Run();
```

`WithReference(loki)` supplies `ConnectionStrings__loki` with Loki's HTTP base URI. Configure your application's logging exporter separately; referencing Loki does not automatically export application logs.

### Example 2: Configure Loki image, ports, and persistent storage

In this approach, Loki uses explicit host ports and a named volume for persistent log storage.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki", settings =>
{
    settings.HttpPort = 13100;
    settings.GrpcPort = 19096;
})
    .WithDataVolume();

builder.Build().Run();
```

Loki listens on container ports 3100 for HTTP and 9096 for gRPC. Readiness is checked through `/ready`. Use `.WithDataBindMount("./loki-data")` for a host directory or `.WithConfigBindMount("./loki.yaml")` for custom configuration.

The data helpers mount `/tmp/loki`. Bind-mounted directories must be writable by the container user (UID 10001). Custom configuration is mounted read-only at `/etc/loki/local-config.yaml`; use storage paths under `/tmp/loki` when combining it with a data mount.

The gRPC endpoint uses cleartext HTTP/2 (`http://`). Set `EnableGrpcEndpoint` to `false` to omit it. The connection string and connection properties always describe the HTTP endpoint, not gRPC.

### TypeScript

The same integration APIs are exported to TypeScript AppHosts:

```typescript
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const loki = await builder.addLoki("loki");
await loki.withDataVolume();

await builder.addProject("api", "../Api/Api.csproj")
    .withReference(loki)
    .waitFor(loki);

await builder.build().run();
```

Use `withDataBindMount("./loki-data")` instead of `withDataVolume()` for host storage, and `withConfigBindMount("./loki.yaml")` for a custom configuration file.

## Connection Properties

`WithReference(loki)` supplies `ConnectionStrings__loki` and the following environment variables for a resource named `loki`:

| Property | Environment variable | Description |
| --- | --- | --- |
| `Host` | `LOKI_HOST` | HTTP endpoint hostname or IP address. |
| `Port` | `LOKI_PORT` | HTTP endpoint port. |
| `Uri` | `LOKI_URI` | HTTP base URI in the form `http://{host}:{port}`; also used as the connection string. |

Values are resolved for the consuming resource, including container-to-container networking.

## Additional documentation

- [Grafana Loki documentation](https://grafana.com/docs/loki/latest/)
- [Aspire documentation](https://aspire.dev/)
- [C# and TypeScript examples](https://github.com/CommunityToolkit/Aspire/tree/main/examples/loki)

## Feedback & contributing

- [Report an issue or request a feature](https://github.com/CommunityToolkit/Aspire/issues)
- [Contributing guide](https://github.com/CommunityToolkit/Aspire/blob/main/CONTRIBUTING.md)