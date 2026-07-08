# Posta hosting integration

Use this integration to model, configure, and orchestrate the [Posta](https://docs.goposta.dev/) self-hosted email delivery platform in an Aspire AppHost.

## Getting started

Install the package in your AppHost project:

```bash
aspire add CommunityToolkit.Aspire.Hosting.Posta
```

## Usage example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("posta-db", "posta");
var redis = builder.AddRedis("redis");

var posta = builder.AddPosta("posta", database, redis, options =>
    {
        options.Environment = "production";
        options.WebUrl = "https://posta.example.com";
        options.CorsOrigins = "https://app.example.com";
        options.MetricsEnabled = true;
    });

builder.AddProject<Projects.Api>("api")
    .WithReference(posta)
    .WaitFor(posta);

builder.Build().Run();
```

Posta stores data in PostgreSQL, uses Redis for queueing, and runs the embedded worker in the main container by default. Pass Aspire-managed PostgreSQL and Redis resources to `AddPosta`, or use `WithReference` to attach them fluently. For externally managed infrastructure, configure `PostaOptions.DatabaseUrl`, `PostaOptions.RedisAddress`, and `PostaOptions.RedisPassword`.

Use the `PostaOptions` callback to configure Posta environment variables such as rate limits, web URLs, OAuth, blob storage, system SMTP, inbound email, email verification, and advanced switches. Secret values such as SMTP passwords, S3 keys, OAuth client secrets, encryption keys, inbound webhook secrets, `POSTA_DB_URL`, and Redis password are represented as Aspire parameters.

## Connection Properties

The Posta resource exposes the following connection properties:

| Name | Format |
| --- | --- |
| `Host` | Posta HTTP API host |
| `Port` | Posta HTTP API port |
| `Uri` | `http://{host}:{port}` |

These properties become environment variables named `[RESOURCE]__HOST`, `[RESOURCE]__PORT`, and `[RESOURCE]__URI` when referenced by another resource. The connection string uses `Endpoint=http://{host}:{port}`.

## Additional documentation

* [Posta documentation](https://docs.goposta.dev/)
* [Posta installation guide](https://docs.goposta.dev/docs/getting-started/installation/)
* [Posta configuration](https://docs.goposta.dev/docs/getting-started/configuration/)

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
