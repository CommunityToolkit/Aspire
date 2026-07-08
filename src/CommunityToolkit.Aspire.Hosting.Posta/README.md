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

var postaWebUrl = builder.AddParameter("posta-web-url");
var postaCorsOrigins = builder.AddParameter("posta-cors-origins");
var postaMetricsEnabled = builder.AddParameter("posta-metrics-enabled");

var posta = builder.AddPosta("posta", database, redis, options =>
    {
        options.WebUrl = postaWebUrl;
        options.CorsOrigins = postaCorsOrigins;
        options.MetricsEnabled = postaMetricsEnabled;
    });

builder.AddProject<Projects.Api>("api")
    .WithReference(posta)
    .WaitFor(posta);

builder.Build().Run();
```

Posta stores data in PostgreSQL, uses Redis for queueing, and runs the embedded worker in the main container by default. `AddPosta` requires PostgreSQL database and Redis resources so the container starts with the dependencies it needs. Use `PostaOptions.DatabaseUrl`, `PostaOptions.RedisAddress`, and `PostaOptions.RedisPassword` only when you need to override the generated Posta environment values.

Use the `PostaOptions` callback to configure Posta environment variables such as rate limits, web URLs, OAuth, blob storage, system SMTP, inbound email, email verification, and advanced switches. `PostaOptions` values are Aspire parameter builders so values can come from configuration, environment variables, user secrets, or publish-time parameters instead of being materialized in the AppHost model. Sensitive values such as SMTP passwords, S3 keys, OAuth client secrets, encryption keys, inbound webhook secrets, `POSTA_DB_URL`, and Redis password should be created as secret parameters.

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
