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
