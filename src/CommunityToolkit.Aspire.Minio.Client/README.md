# CommunityToolkit.Aspire.Minio.Client

Registers a [MiniOClient](https://github.com/minio/minio-dotnet) in the DI container for connecting to MiniO.

## Getting started

### Prerequisites

-   Minio or other S3-compatible storage.

### Install the package

Install the .NET Aspire Minio Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Minio.Client
```

## Usage example

In the _Program.cs_ file of your project, call the `AddMinioClient` extension method to register a `MinioClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddMinioClient();
```

## Configuration

The .NET Aspire Minio Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddMinioClient()`:

```csharp
builder.AddMeilisearchClient("minio");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "minio": "Endpoint=http://localhost:19530/;MasterKey=123456!@#$%"
    }
}
```

### Use configuration providers

The .NET Aspire Meilisearch Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `MeilisearchClientSettings` from configuration by using the `Aspire:Meilisearch:Client` key. Example `appsettings.json` that configures some of the options:

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

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.Minio` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Minio
```

Then, in the _Program.cs_ file of `AppHost`, register a MiniO host and consume the connection using the following methods:

```csharp
var minio = builder.AddMinioContainer("minio");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(minio);
```

The `WithReference` method configures a connection in the `MyService` project named `minio`. In the _Program.cs_ file of `MyService`, the Minio connection can be consumed using:

```csharp
builder.AddMinioClient("minio");
```

Then, in your service, inject `MeilisearchClient` and use it to interact with the Meilisearch API:

```csharp
public class MyService(MeilisearchClient meilisearchClient)
{
    // ...
}
```

## Additional documentation

-   https://github.com/minio/minio-dotnet
-   https://min.io/docs/minio/linux/developers/dotnet/minio-dotnet.html

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

