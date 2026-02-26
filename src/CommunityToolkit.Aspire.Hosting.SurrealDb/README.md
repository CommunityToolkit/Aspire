# CommunityToolkit.Aspire.Hosting.SurrealDb library

Provides extension methods and resource definitions for the Aspire AppHost to support running [SurrealDB](https://surrealdb.com/) containers.

## Getting started

### Install the package

In your AppHost project, install the Aspire SurrealDB Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.SurrealDb
```

## Usage example

Then, in the _Program.cs_ file of `AppHost`, add a SurrealDB resource and consume the connection using the following methods:

```csharp
var db = builder.AddSurrealServer("surreal")
                .AddNamespace("ns")
                .AddDatabase("db");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(db);
```

## Additional Information

https://aspire.dev/integrations/databases/surrealdb/

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

