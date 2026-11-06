# CommunityToolkit.Aspire.Chroma

Provides extension methods for registering a `ChromaClient` of [ChromaDotNet.Client](https://www.nuget.org/packages/ChromaDotNet.Client), which uses the v2 API of Chroma, in a .NET application.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Chroma
```

## Usage

In your application project:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.AddChromaClient("chroma");

// Or using keyed service
builder.AddKeyedChromaClient("chroma");
```

Then resolve the client in your services, and get a client for the records of a collection from it:

```csharp
public class MyService(ChromaClient chromaClient)
{
    public async Task QueryAsync()
    {
        var collection = await chromaClient.GetOrCreateCollectionAsync("movies");
        var collectionClient = chromaClient.GetCollectionClient(collection);
        var results = await collectionClient.QueryAsync(new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f]), nResults: 1);
        // ...
    }
}
```

## Configuration

The client can be configured using connection strings or settings. The connection string is the address of the server, like `http://localhost:8000` or `Endpoint=http://localhost:8000`, and the client adds the path of the v2 API:

```json
{
  "ConnectionStrings": {
    "chroma": "http://localhost:8000"
  },
  "Aspire": {
    "Chroma": {
      "Client": {
        "DisableHealthChecks": false,
        "HealthCheckTimeout": 5000,
        "DisableTracing": false,
        "DisableMetrics": false
      }
    }
  }
}
```

A keyed client reads `Aspire:Chroma:Client:{name}`.

## Health checks

The integration adds a health check that calls the heartbeat of the server: `Chroma` for `AddChromaClient`, and `Chroma_{connectionName}` for `AddKeyedChromaClient`. `DisableHealthChecks` removes it, and `HealthCheckTimeout`, in milliseconds, limits how long it waits.

## Chroma Cloud

The connection string also takes the token, the tenant and the database, as Chroma Cloud needs them:

```
Endpoint=https://api.trychroma.com;Token=<API key>;Tenant=<tenant>;Database=<database>
```

The token goes in the `X-Chroma-Token` header. The settings `Token`, `Tenant` and `Database` in `Aspire:Chroma:Client` give the values the connection string does not have, while the `Endpoint` of a connection string replaces the `Endpoint` of the settings. The settings given in code win over both. In the AppHost, `builder.AddConnectionString("chroma")` passes the connection string to the services that reference it.

## Distributed Chroma

For distributed Chroma, the one Chroma Cloud runs on, installed elsewhere, like with the Helm chart of Chroma, the connection string says so with `Distributed=true`:

```
Endpoint=http://chroma.example:8000;Distributed=true
```

The client then works as on distributed Chroma, as with `WithDistributedChroma` of ChromaDotNet.Client: it sends the indexes added with `ifSupported`, throws before the filters on the documents that distributed Chroma gets wrong, and deletes a collection or a database in one request. The setting `Distributed` in `Aspire:Chroma:Client` does the same. `Distributed=true` in the connection string sets it, while `false` or no value leaves the one of the settings, and the settings given in code win over both. An address of Chroma Cloud is distributed Chroma in any case.

## Traces and metrics

The integration adds the traces and the metrics of ChromaDotNet.Client to OpenTelemetry: a span for each operation, like `query movies`, and the histogram `db.client.operation.duration`. `DisableTracing` and `DisableMetrics` turn them off.
