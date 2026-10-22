# CommunityToolkit.Aspire.RavenDB.Client library

Registers `IDocumentStore` and the associated `IDocumentSession` and `IAsyncDocumentSession` instances in the DI container for connecting to a RavenDB database. Additionally, it enables health checks, metrics, logging, and telemetry.

## Getting started

### Prerequisites

- RavenDB database and connection string for accessing the database or a running RavenDB server instance with its connection details, such as the server's URL and a valid certificate if required.

_**Note:**  
RavenDB allows creating an `IDocumentStore` without a defined database. In such cases, `IDocumentSession` and `IAsyncDocumentSession` will not be available. This library also supports creating a new RavenDB database. However, if you intend to connect to an existing RavenDB database, ensure the database exists and you have its connection details._

### Install the package

Install the `CommunityToolkit.Aspire.RavenDB.Client` library with [NuGet](https://www.nuget.org):  
```dotnetcli
dotnet add package CommunityToolkit.Aspire.RavenDB.Client
```
*To be added once the package is published.*

## Usage example

In the _Program.cs_ file of your project, call the `AddRavenDBClient` extension method to register a `IDocumentStore` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddRavenDBClient("ravendb");
```

You can then retrieve a `IDocumentStore` instance using dependency injection, for example:

```csharp
public class MyService
{
    private readonly IDocumentStore _documentStore;
    public MyService(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    // Your logic here
}
```

## Configuration

The Aspire RavenDB Client component provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddRavenDBClient()`:

```csharp
builder.AddRavenDBClient("ravendb");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
  "ConnectionStrings": {
    "ravendb": "URL=http://localhost:8080;Database=ravenDatabase"
  }
}
```

For a cluster, list its nodes separated by commas, for example `URL=https://a.example.com,https://b.example.com,https://c.example.com`: the client then reaches the cluster even while one node is down.

### Use configuration providers

The Aspire RavenDB Client component supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `RavenDBClientSettings` from configuration by using the `Aspire:RavenDB:Client` key, and then the `Aspire:RavenDB:Client:{connection name}` key.

### Client certificate from configuration

A secured server needs a client certificate. Configuration can give it as a file (`CertificatePath`, with `CertificatePassword` if the file has one) or as the base64-encoded content of a .pfx file (`CertificateBase64`), which suits a certificate delivered as a value, such as an environment variable:

```
Aspire__RavenDB__Client__ravendb__CertificateBase64=MIIK...
```

`Certificate` set in code is used first, then `CertificateBase64`, then `CertificatePath`. The [RavenDB Cloud hosting integration](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.RavenDB.Cloud) delivers every application its certificate this way.

### Use inline delegates

Also you can pass the `Action<RavenDBClientSettings> configureSettings` delegate to set up some or all the options inline, for example to set the database name and certificate from code:

```csharp
builder.AddRavenDBClient("ravendb", settings => 
{
    settings.DatabaseName = "ravenDatabase"; 
    settings.Certificate = ravenCertificate;
});
```

### Use RavenDBClientSettings Class

The RavenDBClientSettings class simplifies configuration by allowing you to specify:
- URLs of your RavenDB nodes.
- Database name to connect to or create.
- Certificate details (via `Certificate`, `CertificateBase64` or `CertificatePath`, with `CertificatePassword`).
- Optional actions to modify the `IDocumentStore`.

Example for creating a new database on a local unsecured RavenDB server:

```csharp
var settings = new RavenDBClientSettings(new[] { “http://127.0.0.1:8080” }, “NorthWind”)
{
	CreateDatabase = true;
};
builder.AddRavenDBClient(settings);
```

You can also configure:
- `DisableHealthChecks` to disable health checks.
- `HealthCheckTimeout` to set the timeout for health checks.
- `DisableTracing` to disable `OpenTelemetry` tracing.

## AppHost extensions

### Install the CommunityToolkit.Aspire.Hosting.RavenDB Library

Install the `CommunityToolkit.Aspire.Hosting.RavenDB` library with [NuGet](https://www.nuget.org):
```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.RavenDB
```
*To be added once the package is published.*

### Usage in AppHost

In your AppHost's _Program.cs_ file, register a RavenDB server resource and consume the connection using the following methods:

```csharp
var ravendb = builder.AddRavenDB("ravendb");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(ravendb);
```

The `WithReference` method configures a connection in the `MyService` project named `ravendb`. In the _Program.cs_ file of `MyService`, the RavenDB connection can be consumed using:

```csharp
builder.AddRavenDBClient("ravendb");
```

## Additional Documentation

- https://ravendb.net/docs
- https://github.com/ravendb/ravendb
- https://learn.microsoft.com/dotnet/aspire/community-toolkit/ravendb

## Feedback & Contributing

https://github.com/CommunityToolkit/Aspire