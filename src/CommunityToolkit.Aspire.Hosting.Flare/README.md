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
                   .WithDataVolumes();

builder.AddProject<Projects.Api>("api")
       .WithOtlpExporter(flare)
       .WaitFor(flare);
```

`AddFlare` runs four containers: the OTLP ingest service (the `flare` resource itself, on the conventional ports `4317` for gRPC and `4318` for HTTP), the query API, the alert worker and the dashboard. ClickHouse (log, trace and metric storage) and Redis (a buffer in front of ClickHouse) are passed in rather than created internally, so they show up in the app model and can be shared or configured, for example with `WithDataVolume`. Flare creates its own database in the ClickHouse server and migrates its own schema on startup.

`WithOtlpExporter(flare)` sets `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL` on the consuming resource. Pass `useHttp: true` to export over OTLP/HTTP instead of gRPC. `WithReference(flare)` is also supported and injects the OTLP/gRPC URL as a connection string.

### Persistence

ClickHouse and Redis keep their data in the volumes you configure on those resources. `WithDataVolumes()` persists Flare's own identity database (dashboard users, sessions and ingest API keys) in a volume shared by the ingest service and the query API. Without it, both keep a private database that is lost when their containers are recreated.

### Other options

| Method | Purpose |
| --- | --- |
| `WithIngestPorts(grpcPort, httpPort)` | Change the host ports of the OTLP endpoints. |
| `WithApiKey(parameter)` | Require an API key on the ingest service. OTLP clients send it as `Authorization: Bearer <key>` (for example `OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer <key>`). |
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
