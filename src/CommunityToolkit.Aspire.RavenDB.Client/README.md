# CommunityToolkit.Aspire.RavenDB.Client library

Registers `IDocumentStore` and the associated `IDocumentSession` and `IAsyncDocumentSession` instances in the DI container for connecting to a RavenDB database. Additionally, it enables health checks, metrics, logging, and telemetry.

## Getting started

### Prerequisites

- A running RavenDB server instance with its connection details, such as the server's URL and a valid certificate if required.

_**Note:**  
RavenDB allows creating an `IDocumentStore` without a defined database. In such cases, `IDocumentSession` and `IAsyncDocumentSession` will not be available. This library also supports creating a new RavenDB database. However, if you intend to connect to an existing RavenDB database, ensure the database exists and you have its connection details._

### Install the package

Install the `CommunityToolkit.Aspire.RavenDB.Client` library with [NuGet](https://www.nuget.org):  
```dotnetcli
dotnet add package CommunityToolkit.Aspire.RavenDB.Client
```
*To be added once the package is published.*

## Usage example

In the _Program.cs_ file of your project, use the `AddRavenDBClient` or `AddKeyedRavenDBClient` extension methods to register `IDocumentStore` and the associated `IDocumentSession` and `IAsyncDocumentSession` instances for dependency injection. Use `AddKeyedRavenDBClient` for scenarios requiring multiple RavenDB connections distinguished by service keys.

```csharp
var settings = new RavenDBSettings(new[] { defaultConnectionString }, databaseName);
builder.AddRavenDBClient(settings);
var host = builder.Build();
```
Once the host is built, you can retrieve the registered services:

```csharp
var documentStore = host.Services.GetRequiredService<IDocumentStore>();
var session = host.Services.GetRequiredService<IDocumentSession>();
var asyncSession = host.Services.GetRequiredService<IAsyncDocumentSession>();
```

## Configuration

The `CommunityToolkit.Aspire.RavenDB.Client` library provides multiple options to configure the database connection to suit your project's needs.

### RavenDBClientSettings Class

The RavenDBClientSettings class simplifies configuration by allowing you to specify:
- URLs of your RavenDB nodes.
- Database name to connect to or create.
- Certificate details (via `CertificatePath` and `CertificatePassword` or `Certificate`).
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

Alternatively, use overload methods to pass configuration parameters directly, such as connection URLs, database name, certificate, or a service key.

## AppHost extensions

For applications where you need a RavenDB container to run alongside your application, consider using the `CommunityToolkit.Aspire.Hosting.RavenDB` library. This is particularly useful for local development or when you want to manage the RavenDB server lifecycle via code.

### Install the CommunityToolkit.Aspire.Hosting.RavenDB Library

Install the `CommunityToolkit.Aspire.Hosting.RavenDB` library with [NuGet](https://www.nuget.org):
```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.RavenDB
```
*To be added once the package is published.*

### Usage in AppHost

In your AppHost's _Program.cs_ file, register and start a RavenDB container:

```csharp
var ravendb = builder.AddRavenDB("ravendb", serverSettings).AddDatabase("mydatabase");
builder.Build().Run();
```
From your client application, configure `RavenDBClientSettings` to match the server settings and connect using:

```csharp
builder.AddRavenDBClient("ravendb", clientSettings);
```

## Additional Documentation

- https://ravendb.net/docs/article-page/6.2/csharp
- https://github.com/ravendb/ravendb
- https://learn.microsoft.com/dotnet/aspire/community-toolkit/ravendb <!-- TODO: Update the link once it is created -->

## Feedback & Contributing

https://github.com/CommunityToolkit/Aspire