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

## Chroma Cloud

The connection string also takes the token, the tenant and the database, as Chroma Cloud needs them:

```
Endpoint=https://api.trychroma.com;Token=<API key>;Tenant=<tenant>;Database=<database>
```

The token goes in the `X-Chroma-Token` header. The settings `Token`, `Tenant` and `Database` in `Aspire:Chroma:Client` give the values the connection string does not have, and the settings given in code win over both. In the AppHost, `builder.AddConnectionString("chroma")` passes the connection string to the services that reference it.

## Traces and metrics

The integration adds the traces and the metrics of ChromaDotNet.Client to OpenTelemetry: a span for each operation, like `query movies`, and the histogram `db.client.operation.duration`. `DisableTracing` and `DisableMetrics` turn them off.
