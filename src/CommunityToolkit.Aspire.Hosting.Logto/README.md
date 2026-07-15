# Logto hosting integration

Use this integration to model, configure, and orchestrate a [Logto](https://logto.io/) identity service in an Aspire distributed application. It connects Logto to PostgreSQL, optionally connects Redis, exposes the public and Admin Console endpoints, and provides a one-shot database setup command for local runs.

## Getting started

Install the hosting integration in the AppHost directory:

```console
aspire add CommunityToolkit.Aspire.Hosting.Logto
```

## Usage example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var redis = builder.AddRedis("redis");

var logto = builder.AddLogto("logto", postgres)
    .WithRedis(redis)
    .WithDatabaseSeeding();

builder.AddProject<Projects.MyApi>("api")
    .WithReference(logto)
    .WaitFor(logto);

builder.Build().Run();
```

The same integration is exported to a TypeScript AppHost:

```typescript
const postgres = await builder.addPostgres("postgres");
const redis = await builder.addRedis("redis");
const logto = await builder.addLogto("logto", postgres);
const api = await builder.addProject("api", "../MyApi/MyApi.csproj");

await logto.withRedis(redis);
await logto.withDatabaseSeeding();
await api.withReference(logto);
await api.waitFor(logto);
```

`AddLogto` configures `DB_URL`, `ENDPOINT`, `ADMIN_ENDPOINT`, the `/api/status` health check, and the dependency on PostgreSQL. `WithRedis` adds `REDIS_URL` and the Redis dependency.

### Database setup

Call `WithDatabaseSeeding()` to seed the database and deploy required schema alterations in a one-shot setup container before Logto starts. The helper is run-only and is excluded from published manifests. Repeated calls do not create duplicate setup resources.

For an air-gapped environment, use `WithDatabaseSeeding(disableAdminPwnedPasswordCheck: true)` to pass Logto's `--dapc` option during initial administrator creation.

### Local Admin Console and CORS

Logto's API and Admin Console use separate endpoints. During local Aspire runs, the integration sets `ENDPOINT` to the allocated API URL and normalizes the Admin Console URL to `127.0.0.1`. This is required by Logto 1.41's production CORS behavior.

Open the exact `admin` URL shown in the Aspire Dashboard. Replacing `127.0.0.1` with `localhost` can cause requests such as `/api/resources` to fail CORS preflight checks and prevent Applications from being created. `WithAdminEndpoint("https://admin.example.com")` updates both Logto's `ADMIN_ENDPOINT` and the Dashboard URL.

When TLS terminates at a reverse proxy, configure `WithTrustProxyHeader(true)` and ensure that the proxy sends `X-Forwarded-Proto`.

## Connection properties

When another resource uses `WithReference(logto)`, Aspire injects the connection string in the standard `ConnectionStrings__{resourceName}` environment variable. The connection string has the form `Endpoint={Uri}`.

| Property | Description | Example |
| --- | --- | --- |
| `Host` | Host name of the public Logto endpoint. | `localhost` |
| `Port` | Allocated port of the public Logto endpoint. | `10611` |
| `Uri` | Complete public Logto endpoint URL. | `http://localhost:10611` |

The Admin Console endpoint is intentionally excluded from service-reference endpoint expansion; consumers receive the public Logto endpoint only.

## Additional documentation

- [Logto documentation](https://docs.logto.io/)
- [Logto configuration](https://docs.logto.io/logto-oss/using-cli/logto-config)
- [Aspire service discovery](https://learn.microsoft.com/dotnet/aspire/service-discovery/overview)

## Feedback & contributing

Report bugs and request features in the [CommunityToolkit/Aspire issue tracker](https://github.com/CommunityToolkit/Aspire/issues). Contributions are welcome; see the repository's [contributing guide](https://github.com/CommunityToolkit/Aspire/blob/main/CONTRIBUTING.md).
