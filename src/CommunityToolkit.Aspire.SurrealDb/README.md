# CommunityToolkit.Aspire.SurrealDb

Registers a [SurrealDbClient](https://github.com/surrealdb/surrealdb.net) in the DI container for connecting to a SurrealDB instance.

## Getting started

### Prerequisites

-   SurrealDB cluster.

### Install the package

Install the .NET Aspire SurrealDB Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.SurrealDb
```

## Usage example

In the _Program.cs_ file of your project, call the `AddSurrealClient` extension method to register a `SurrealDbClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddSurrealClient("surreal");
```

## Configuration

The .NET Aspire SurrealDB Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddSurrealClient()`:

```csharp
builder.AddSurrealClient("surreal");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "surreal": "Server=ws://127.0.0.1:8000/rpc;Namespace=test;Database=test;Username=root;Password=root"
    }
}
```

### Use configuration providers

The .NET Aspire SurrealDB Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `SurrealDbClientSettings` from configuration by using the `Aspire:Surreal:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "Surreal": {
      "Client": {
        "Options": {
          "Endpoint": "ws://127.0.0.1:8000/rpc", 
          "Namespace": "test",
          "Database": "test",
          "Username": "root",
          "Password": "root"
        }
      }
    }
  }
}
```

### Use inline delegates

Also you can pass the `Action<SurrealDbClientSettings> configureSettings` delegate to set up some or all the options inline, for example to set the API key from code:

```csharp
builder.AddSurrealDbClient("surreal", settings => settings.Options.Endpoint = "ws://localhost:8000/rpc");
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.SurrealDb` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.SurrealDb
```

Then, in the _Program.cs_ file of `AppHost`, register a SurrealDB cluster and consume the connection using the following methods:

```csharp
var db = builder.AddSurrealServer("surreal")
                .AddNamespace("ns")
                .AddDatabase("db");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(db);
```

The `WithReference` method configures a connection in the `MyService` project named `db`. In the _Program.cs_ file of `MyService`, the SurrealDB connection can be consumed using:

```csharp
builder.AddSurrealClient("db");
```

Then, in your service, inject `SurrealDbClient` and use it to interact with the SurrealDB instance:

```csharp
public class MyService(SurrealDbClient client)
{
    // ...
}
```

## Additional documentation

-   https://github.com/surrealdb/surrealdb.net
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-surrealdb

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

