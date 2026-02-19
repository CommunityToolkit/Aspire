# CommunityToolkit.Aspire.Hosting.Chroma

Provides extension methods for adding a ChromaDB resource to a .NET Aspire application model.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Chroma
```

## Usage

In your AppHost's `Program.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var chroma = builder.AddChroma("chroma");

builder.AddProject<Projects.MyWebApp>()
       .WithReference(chroma);

builder.Build().Run();
```

## Persistence

You can configure persistence using either Docker volumes or bind mounts:

```csharp
// Using a Docker volume
var chroma = builder.AddChroma("chroma")
                    .WithDataVolume();

// Using a bind mount
var chroma = builder.AddChroma("chroma")
                    .WithDataBindMount("C:/chroma/data");
```
