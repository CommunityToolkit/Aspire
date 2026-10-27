# CommunityToolkit.Aspire.Hosting.Chroma

Provides extension methods for adding a ChromaDB resource to an Aspire application model. The resource runs the `chromadb/chroma` container image, with a health check on the `/api/v2/heartbeat` endpoint.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Chroma
```

## Usage

In your AppHost's `Program.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var chroma = builder.AddChroma("chroma");

builder.AddProject<Projects.MyWebApp>("mywebapp")
       .WithReference(chroma);

builder.Build().Run();
```

`AddChroma("chroma")` adds a container resource named `chroma`, with the HTTP endpoint `http` on port 8000 of the container. The port on the host changes at each run; `AddChroma("chroma", port: 8000)` sets it. A resource that references it gets the connection string `ConnectionStrings__chroma`, `Endpoint=http://{host}:{port}`, and the variables `CHROMA_HOST`, `CHROMA_PORT` and `CHROMA_URI`: the values of the `Host`, `Port` and `UriExpression` properties of the resource.

### TypeScript AppHost

```ts
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const chroma = await builder.addChroma("chroma", { port: 8000 });
await chroma.withDataVolume({ name: "chroma-data" });

await builder.build().run();
```

`withDataBindMount(source)` adds a bind mount, and `host()`, `port()` and `uriExpression()` give the connection properties.

## Chroma versions

The default image is `chromadb/chroma:1.5.9`. `WithImageTag` picks another one, from Chroma 0.5.16: the health check calls `/api/v2/heartbeat`, which older images answer with `404`, so their resource never becomes healthy.

## Persistence

You can configure persistence using either a Docker volume:

```csharp
var chroma = builder.AddChroma("chroma")
                    .WithDataVolume();
```

or a bind mount:

```csharp
var chroma = builder.AddChroma("chroma")
                    .WithDataBindMount("C:/chroma/data");
```

## Logs

While the AppHost runs, the resource sets `CHROMA_STDOUT_TRACING` to `true`, so Chroma writes the requests it serves to its log, which the dashboard shows. A published app does not get the variable. To turn the request logs off:

```csharp
var chroma = builder.AddChroma("chroma")
                    .WithEnvironment("CHROMA_STDOUT_TRACING", "false");
```

## Known limitations

The integration does not send the telemetry of the Chroma server to the dashboard. Chroma 1.5.9 sends its OTLP traces as gRPC without TLS, also to an `https://` endpoint like the default one of the dashboard.
