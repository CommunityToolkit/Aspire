# CommunityToolkit.Aspire.KurrentDB

Registers an [KurrentDBClient](https://github.com/kurrent-io/KurrentDB-Client-Dotnet) in the DI container for connecting to KurrentDB.

## Getting started

### Prerequisites

-   KurrentDB cluster.

### Install the package

Install the Aspire KurrentDB Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.KurrentDB
```

## Usage example

In the _Program.cs_ file of your project, call the `AddKurrentDBClient` extension method to register an `KurrentDBClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddKurrentDBClient("kurrentdb");
```

## Configuration

The Aspire KurrentDB Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddKurrentDBClient()`:

```csharp
builder.AddKurrentDBClient("kurrentdb");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "kurrentdb": "kurrentdb://localhost:22113?tls=false"
    }
}
```

### Use configuration providers

The Aspire KurrentDB Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `KurrentDBSettings` from configuration by using the `Aspire:KurrentDB:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "KurrentDB": {
      "Client": {
        "ConnectionString": "kurrentdb://localhost:22113?tls=false",
        "DisableHealthChecks": true
      }
    }
  }
}
```

### Use inline delegates

Also you can pass the `Action<KurrentDBSettings> configureSettings` delegate to set up some or all the options inline, for example to set the API key from code:

```csharp
builder.AddKurrentDBClient("kurrentdb", settings => settings.DisableHealthChecks = true);
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.KurrentDB` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.KurrentDB
```

Then, in the _Program.cs_ file of `AppHost`, register KurrentDB and consume the connection using the following methods:

```csharp
var kurrentdb = builder.AddKurrentDB("kurrentdb");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(kurrentdb);
```

The `WithReference` method configures a connection in the `MyService` project named `kurrentdb`. In the _Program.cs_ file of `MyService`, the KurrentDB connection can be consumed using:

```csharp
builder.AddKurrentDBClient("kurrentdb");
```

Then, in your service, inject `KurrentDBClient` and use it to interact with the KurrentDB API:

```csharp
public class MyService(KurrentDBClient client)
{
    // ...
}
```

## Additional documentation

-   https://github.com/kurrent-io/KurrentDB-Client-Dotnet
-   https://www.kurrent.io
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-kurrentdb

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

