# CommunityToolkit.Aspire.Meilisearch

Registers a [MeilisearchClient](https://github.com/meilisearch/meilisearch-dotnet) in the DI container for connecting to a Meilisearch.

## Getting started

### Prerequisites

-   Meilisearch cluster.

### Install the package

Install the Aspire Meilisearch Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Meilisearch
```

## Usage example

In the _Program.cs_ file of your project, call the `AddMeilisearchClient` extension method to register a `MeilisearchClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddMeilisearchClient("meilisearch");
```

## Configuration

The Aspire Meilisearch Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddMeilisearchClient()`:

```csharp
builder.AddMeilisearchClient("meilisearch");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "meilisearch": "Endpoint=http://localhost:19530/;MasterKey=123456!@#$%"
    }
}
```

### Use configuration providers

The Aspire Meilisearch Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `MeilisearchClientSettings` from configuration by using the `Aspire:Meilisearch:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "Meilisearch": {
      "Client": {
        "Endpoint": "http://localhost:19530/",
        "MasterKey": "123456!@#$%"
      }
    }
  }
}
```

### Use inline delegates

Also you can pass the `Action<MeilisearchClientSettings> configureSettings` delegate to set up some or all the options inline, for example to set the API key from code:

```csharp
builder.AddMeilisearchClient("meilisearch", settings => settings.MasterKey = "123456!@#$%");
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.Meilisearch` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Meilisearch
```

Then, in the _Program.cs_ file of `AppHost`, register a Meilisearch cluster and consume the connection using the following methods:

```csharp
var meilisearch = builder.AddMeilisearch("meilisearch");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(meilisearch);
```

The `WithReference` method configures a connection in the `MyService` project named `meilisearch`. In the _Program.cs_ file of `MyService`, the Meilisearch connection can be consumed using:

```csharp
builder.AddMeilisearchClient("meilisearch");
```

Then, in your service, inject `MeilisearchClient` and use it to interact with the Meilisearch API:

```csharp
public class MyService(MeilisearchClient meilisearchClient)
{
    // ...
}
```

## Additional documentation

-   https://github.com/meilisearch/meilisearch-dotnet
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-meilisearch

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

