# CommunityToolkit.Aspire.Hosting.OpenFga

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [OpenFGA](https://openfga.dev/) containers.

OpenFGA is an open-source authorization solution that allows developers to build granular access control using a flexible authorization model.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.OpenFga
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an OpenFGA resource, then call `AddOpenFga`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add OpenFGA with in-memory storage (for development)
var openfga = builder.AddOpenFga("openfga")
    .WithInMemoryDatastore();

// Or use PostgreSQL for persistent storage
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("openfga-db");

var openfga = builder.AddOpenFga("openfga")
    .WithPostgresDatastore(postgres);

// Reference the OpenFGA server in your projects
builder.AddProject<Projects.MyApi>("api")
    .WithReference(openfga);

builder.Build().Run();
```

## Configuration

### Datastore Options

OpenFGA supports multiple datastore backends:

#### In-Memory (Development Only)

```csharp
var openfga = builder.AddOpenFga("openfga")
    .WithInMemoryDatastore();
```

**Note:** In-memory storage is suitable for development and testing only. Data will be lost when the container restarts.

#### PostgreSQL

```csharp
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("openfga-db");

var openfga = builder.AddOpenFga("openfga")
    .WithPostgresDatastore(postgres);
```

#### MySQL

```csharp
var mysql = builder.AddMySql("mysql")
    .AddDatabase("openfga-db");

var openfga = builder.AddOpenFga("openfga")
    .WithMySqlDatastore(mysql);
```

### Data Volume

To persist OpenFGA data across container restarts, you can add a data volume:

```csharp
var openfga = builder.AddOpenFga("openfga")
    .WithDataVolume();
```

### Custom Ports

You can specify custom ports for the HTTP and gRPC endpoints:

```csharp
var openfga = builder.AddOpenFga("openfga", httpPort: 8080, grpcPort: 8081);
```

### Experimental Features

OpenFGA supports experimental features that can be enabled:

```csharp
var openfga = builder.AddOpenFga("openfga")
    .WithExperimentalFeatures("enable-list-users");
```

## Image Versioning

This integration uses the `openfga/openfga:v1.8.5` Docker image by default. The image version is pinned to ensure reproducible builds. You can override the image version using the standard Aspire methods:

```csharp
var openfga = builder.AddOpenFga("openfga")
    .WithImage("openfga/openfga", "v1.9.0");
```

## Additional Information

For more information about OpenFGA, visit the [official documentation](https://openfga.dev/docs).

## Feedback & Contributing

https://github.com/CommunityToolkit/Aspire
