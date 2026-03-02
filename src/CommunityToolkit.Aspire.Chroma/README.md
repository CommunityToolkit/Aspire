# CommunityToolkit.Aspire.Chroma

Provides extension methods for registering a `ChromaClient` in a .NET application.

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

Then resolve the client in your services:

```csharp
public class MyService(ChromaClient chromaClient)
{
    public async Task QueryAsync()
    {
        var version = await chromaClient.GetVersion();
        // ...
    }
}
```

## Configuration

The client can be configured using connection strings or settings:

```json
{
  "ConnectionStrings": {
    "chroma": "http://localhost:8000"
  },
  "Aspire": {
    "Chroma": {
      "DisableHealthChecks": false,
      "HealthCheckTimeout": 5000
    }
  }
}
```
