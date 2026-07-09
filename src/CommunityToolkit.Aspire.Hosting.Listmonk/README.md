# listmonk hosting integration

Use this integration to model, configure, and orchestrate a listmonk newsletter and mailing list manager in an Aspire AppHost. The integration runs listmonk in a container and configures it to use a PostgreSQL database resource.

## Getting started

Install the package in your AppHost project:

```shell
aspire add CommunityToolkit.Aspire.Hosting.Listmonk
```

## Usage example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var adminPassword = builder.AddParameter("listmonk-admin-password", secret: true);
var database = builder.AddPostgres("postgres")
    .AddDatabase("listmonkdb");

var listmonk = builder.AddListmonk("listmonk")
    .WithReference(database)
    .WithAdminCredentials("admin", adminPassword)
    .WithTimeZone("Etc/UTC")
    .WithUploadsVolume();

builder.AddProject<Projects.Api>("api")
    .WithReference(listmonk);

builder.Build().Run();
```

## Connection Properties

The `ListmonkResource` exposes the following connection properties:

| Name | Format |
| --- | --- |
| `Host` | The listmonk host name |
| `Port` | The listmonk HTTP port |
| `Uri` | `http://{host}:{port}` |

Connection properties become environment variables when referenced, for example `LISTMONK_URI`.

## Configuration

The integration exposes listmonk's container configuration environment variables as fluent methods:

| Method | Environment variable |
| --- | --- |
| `WithAppAddress` | `LISTMONK_app__address` |
| `WithReference` | `LISTMONK_db__host`, `LISTMONK_db__port`, `LISTMONK_db__user`, `LISTMONK_db__password`, `LISTMONK_db__database`, `LISTMONK_db__ssl_mode` |
| `WithDatabaseSslMode` | `LISTMONK_db__ssl_mode` |
| `WithDatabaseMaxOpenConnections` | `LISTMONK_db__max_open` |
| `WithDatabaseMaxIdleConnections` | `LISTMONK_db__max_idle` |
| `WithDatabaseMaxLifetime` | `LISTMONK_db__max_lifetime` |
| `WithDatabaseParameters` | `LISTMONK_db__params` |
| `WithTimeZone` | `TZ` |
| `WithAdminUser`, `WithAdminPassword`, `WithAdminCredentials` | `LISTMONK_ADMIN_USER`, `LISTMONK_ADMIN_PASSWORD` |
| `WithUserId`, `WithGroupId` | `PUID`, `PGID` |

The admin credentials are only used by listmonk during first database setup.
listmonk's container entrypoint also supports `LISTMONK_*_FILE` variables for Docker or Podman secret files. Prefer Aspire parameters for secrets in AppHost code; if a deployment requires file-based secret paths, configure those specific variables with Aspire's generic `WithEnvironment` API.

## Additional documentation

See the [listmonk documentation](https://listmonk.app/docs/) for listmonk configuration, administration, and operation.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
