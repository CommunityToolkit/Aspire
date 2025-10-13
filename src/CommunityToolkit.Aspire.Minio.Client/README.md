# CommunityToolkit.Aspire.Minio.Client

Registers a [MinIOClient](https://github.com/minio/minio-dotnet) in the DI container for connecting to MinIO.

## Getting started

### Prerequisites

-   MinIO or other S3-compatible storage.

### Install the package

Install the .NET Aspire MinIO Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Minio.Client
```

## Usage example

In the _Program.cs_ file of your project, call the `AddMinioClient` extension method to register a `MinioClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddMinioClient("minio");
```

## Configuration

The .NET Aspire MinIO Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddMinioClient()`:

```csharp
builder.AddMinioClient("minio");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "minio": "Endpoint=http://localhost:9001/;AccessKey=minioAdmin;SecretKey=minioAdmin"
    }
}
```

### Use configuration providers

The .NET Aspire MinIO Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration).
It loads the `MinioClientSettings` from configuration by using the `Aspire:Minio:Client` key.
This key can be overriden by using the `configurationSectionName` method parameter.
Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "Minio": {
      "Client": {
        "Endpoint": "http://localhost:9001/",
        "AccessKey": "minioAdmin",
        "SecretKey": "minioAdmin"
      }
    }
  }
}
```

### Use inline delegates

Also you can pass the `Action<MinioClientSettings> configureSettings` delegate to set up some or all the options inline, for example to set the API key from code:

```csharp
builder.AddMinioClient("minio", configureSettings: settings => settings.SecretKey = "minioAdmin");
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.Minio` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Minio
```

Then, in the _Program.cs_ file of `AppHost`, register a MinIO host and consume the connection using the following methods:

```csharp
var minio = builder.AddMinioContainer("minio");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(minio);
```

The `WithReference` method configures a connection in the `MyService` project named `minio`.
In the _Program.cs_ file of `MyService`, the MinIO connection can be consumed using:

```csharp
builder.AddMinioClient("minio");
```

Then, in your service, inject `IMinioClient` and use it to interact with the MinIO or other S3 compatible API:

```csharp
public class MyService(IMinioClient minioClient)
{
    // ...
}
```

## Additional documentation

-   https://github.com/minio/minio-dotnet
-   https://min.io/docs/minio/linux/developers/dotnet/minio-dotnet.html

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

