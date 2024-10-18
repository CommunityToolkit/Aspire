# Aspire.CommunityToolkit.Hosting.Meilisearch library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [Meilisearch](https://meilisearch.com) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package Aspire.CommunityToolkit.Hosting.Meilisearch
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, add a Meilisearch resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var meilisearch = builder.AddMeilisearch("meilisearch");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(meilisearch);

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-meilisearch

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
