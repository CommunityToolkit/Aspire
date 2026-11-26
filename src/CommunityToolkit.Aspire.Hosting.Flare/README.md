# CommunityToolkit.Aspire.Hosting.Flare library

Use this integration to model, configure, and orchestrate [Flare](https://github.com/aminparsa18/Flare.Net) in your Aspire AppHost. Flare is a self-hosted, OpenTelemetry-native observability backend (logs, traces, metrics and alerting) for .NET. Applications keep using the standard OpenTelemetry SDK and send plain OTLP to it.

Flare is not a replacement for the Aspire dashboard. Use it where the dashboard stops: telemetry that persists across runs, alert rules (Slack, webhook, email, PagerDuty and more), and the same backend carried through `aspire publish`.

## Getting started

### Install the package

In your AppHost project, install the Aspire Flare Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Flare
```

Or add it with the Aspire CLI:

```bash
aspire add CommunityToolkit.Aspire.Hosting.Flare
```

## Usage example

Then, in the _AppHost.cs_ file of your AppHost, add Flare on top of a ClickHouse server and a Redis instance, and point your projects at it:

```csharp
var clickhouse = builder.AddClickHouse("clickhouse");
var redis = builder.AddRedis("redis");

var flare = builder.AddFlare("flare", clickhouse, redis)
                   .WithDataVolume();

builder.AddProject<Projects.Api>("api")
       .WithOtlpExporter(flare)
       .WaitFor(flare);
```

`AddFlare` runs four containers: the OTLP ingest service (the `flare` resource itself, on the conventional ports `4317` for gRPC and `4318` for HTTP), the query API, the alert worker and the dashboard. ClickHouse (log, trace and metric storage) and Redis (a buffer in front of ClickHouse) are passed in rather than created internally, so they show up in the app model and can be shared or configured, for example with `WithDataVolume`. Flare creates its own database in the ClickHouse server and migrates its own schema on startup.

`WithOtlpExporter(flare)` sets `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL` on the consuming resource. It exports over OTLP/gRPC by default; pass an `OtlpProtocol`, for example `WithOtlpExporter(flare, OtlpProtocol.HttpProtobuf)`, to use OTLP/HTTP with protobuf or JSON (`OtlpProtocol.HttpJson`) instead. `WithReference(flare)` is also supported and injects the OTLP/gRPC URL as a connection string.

A resource wired with `WithOtlpExporter(flare)` sends its telemetry to Flare **instead of** the Aspire dashboard, so its logs, traces and metrics no longer appear in the dashboard's telemetry views (the resource itself, its console output and its state still do). To see the same telemetry in both, export to an OpenTelemetry Collector that forwards to each. `WithOtlpExporter` also removes the Aspire dashboard's OTLP API key from `OTEL_EXPORTER_OTLP_HEADERS`, so that key is not sent to Flare, and adds `Authorization=Bearer <key>` when Flare has an API key (see `WithApiKey`).

### Persistence

ClickHouse and Redis keep their data in the volumes you configure on those resources. `WithDataVolume()` persists Flare's own identity database (dashboard users, sessions and ingest API keys) in a volume shared by the ingest service and the query API. Without it, both keep a private database that is lost when their containers are recreated.

`WithIdentityDatabase(database)` keeps the identity database in PostgreSQL instead. The ingest service and the query API then share a database rather than a file, so they can run on different nodes. Use it when you publish to a Kubernetes cluster with more than one node, where a volume shared by two workloads usually can't be mounted on both. Any resource with an Npgsql connection string works:

```csharp
var identity = builder.AddPostgres("postgres")
                      .WithDataVolume()
                      .AddDatabase("flare-identity");

var flare = builder.AddFlare("flare", clickhouse, redis)
                   .WithIdentityDatabase(identity);
```

### Cold storage

`WithColdStorage(endpoint, accessKey, secretKey)` adds an S3-compatible bucket to Flare's ClickHouse server as a cold tier, so retention can move older telemetry off ClickHouse's local disk. It works with RustFS, MinIO, Amazon S3 or any other S3-compatible store. The endpoint is the bucket's path-style URL and must end with a slash. Nothing moves to the bucket until you set a cold-after age in Flare's retention settings.

The bucket must exist before ClickHouse starts, because ClickHouse stops at startup if it can't reach a disk. For local development, the [RustFS integration](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.RustFs) can create the bucket, and ClickHouse can wait for it:

```csharp
var rustfs = builder.AddRustFs("rustfs").WithDataVolume();
var bucket = rustfs.AddBucket("flare-cold");

var clickhouse = builder.AddClickHouse("clickhouse").WaitFor(bucket);
var redis = builder.AddRedis("redis");

var flare = builder.AddFlare("flare", clickhouse, redis)
                   .WithColdStorage(
                       ReferenceExpression.Create($"{rustfs.Resource.PrimaryEndpoint}/flare-cold/"),
                       builder.CreateResourceBuilder(rustfs.Resource.AccessKey),
                       builder.CreateResourceBuilder(rustfs.Resource.SecretKey));
```

The ClickHouse disk configuration is added with `WithContainerFiles`, which the Kubernetes publisher doesn't support yet. Without the configuration ClickHouse runs normally, and Flare reports cold storage as unavailable.

### Other options

| Method | Purpose |
| --- | --- |
| `WithIngestPorts(grpcPort, httpPort)` | Change the host ports of the OTLP endpoints. |
| `WithApiKey(parameter)` | Require an API key on the ingest service. OTLP clients send it as `Authorization: Bearer <key>` (for example `OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer <key>`); resources wired with `WithOtlpExporter(flare)` send it automatically. |
| `WithPublicUrls(apiUrl, dashboardUrl)` | Set the URLs a browser uses once the stack is published and no longer reachable on localhost. `dashboardUrl` is also the API's allowed CORS origin, so pass an origin only (`https://flare.example.com`, no path or trailing slash). |
| `WithFlareImageTag(tag)` | Change the image tag of all four containers. |

## Connection Properties

When you reference the Flare resource using `WithReference`, the following connection properties are made available to the consuming project:

| Property Name | Description |
| --- | --- |
| `Host` | The host of the OTLP/gRPC endpoint |
| `Port` | The port of the OTLP/gRPC endpoint |
| `Uri` | The OTLP/gRPC URL, for example `http://localhost:4317` |

The connection properties are exposed as environment variables named `[RESOURCE]_[PROPERTY]`, for example `FLARE_URI`.

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-flare

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
