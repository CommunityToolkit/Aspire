# CommunityToolkit.Hosting.Azure.DataApiBuilder

## Overview

This .NET Aspire Integration runs [Data API builder](https://aka.ms/dab/docs) in a container. Data API builder generates secure, feature-rich REST and GraphQL endpoints for Tables, Views and Stored Procedures performing CRUD (Create, Read, Update, Delete, Execute) operations against Azure SQL Database, SQL Server, PostgreSQL, MySQL and Azure CosmosDB. 

## Usage

### Example 1: Single data source

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder
    .AddSqlServer("your-server-name")
    .AddDatabase("your-database-name");

var dab = builder.AddDataAPIBuilder("dab")
    .WithReference(sqlDatabase)
    .WaitFor(sqlDatabase);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

### Example 2: Multiple data sources

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase1 = builder
    .AddSqlServer("your-server-name")
    .AddDatabase("your-database-name");

var sqlDatabase2 = builder
    .AddSqlServer("your-server-name")
    .AddDatabase("your-database-name");

var dab = builder.AddDataAPIBuilder("dab", 
        "./dab-config-1.json", 
        "./dab-config-2.json")
    .WithReference(sqlDatabase1)
    .WithReference(sqlDatabase2)
    .WaitFor(sqlDatabase1)
    .WaitFor(sqlDatabase2);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

> Note: All files are mounted/copied to the same `/App` folder.

### Example 3: Cosmos DB and a schema file

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cosmosdb = builder
    .AddAzureCosmosDB("myNewCosmosAccountName")
    .AddDatabase("myCosmosDatabaseName");

var dab = builder.AddDataAPIBuilder("dab",
        "./dab-config.json",
        "./schema.graphql")
    .WithReference(cosmosdb)
    .WaitFor(cosmosdb);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

### Example 4: Connection string-only

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder
    .AddConnectionString("your-cs-name");

var dab = builder.AddDataAPIBuilder("dab")
    .WithReference(sqlDatabase);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

### Configuration

- `name` - The name of the resource.
- `port` - The optional port number for the Data API builder container. Defaults to `random`.
- `configFilePaths` - Opiotnal paths to the config/schema file(s) for Data API builder. Default is `./dab-config.json`.

### Data API builder Container Image Configuration

You can specify custom registry/image/tag values by using the `WithImageRegistry`/`WithImage`/`WithImageTag` methods:

```csharp
var dab = builder.AddDataAPIBuilder("dab")
    .WithImageRegistry("mcr.microsoft.com")
    .WithImage("azure-databases/data-api-builder")
    .WithImageTag("latest");
```

### OpenTelemetry Instrumentation

The Data API builder integration automatically configures OpenTelemetry (OTEL) instrumentation for distributed tracing and metrics. The integration uses the standard `.WithOtlpExporter()` method which sets up the necessary OTEL environment variables that Data API builder automatically recognizes.

To enable OTEL telemetry in Data API builder, add the following configuration to your `dab-config.json` file:

```json
{
  "runtime": {
    "telemetry": {
      "open-telemetry": {
        "enabled": true,
        "service-name": "@env('OTEL_SERVICE_NAME')",
        "endpoint": "@env('OTEL_EXPORTER_OTLP_ENDPOINT')",
        "exporter-protocol": "grpc",
        "headers": "@env('OTEL_EXPORTER_OTLP_HEADERS')"
      }
    }
  }
}
```

With this configuration, Data API builder will:
- Export traces and metrics to the Aspire dashboard via OTLP (OpenTelemetry Protocol)
- Automatically use the OTEL endpoint provided by the Aspire app host
- Include telemetry for REST and GraphQL operations, database queries, and system metrics

#### Advanced OTEL Configuration

Data API builder supports additional OTEL configuration options in the `dab-config.json` file:

```json
{
  "runtime": {
    "telemetry": {
      "open-telemetry": {
        "enabled": true,
        "service-name": "my-dab-service",
        "exporter-protocol": "grpc"
      }
    }
  }
}
```

Available settings:
- `enabled`: Enables/disables OTEL telemetry (default: `false`)
- `service-name`: Logical name for the service in traces (optional, defaults to the resource name)
- `endpoint`: OTEL collector endpoint URL (optional, automatically set by Aspire)
- `exporter-protocol`: Choose between `grpc` or `httpprotobuf` (optional, defaults to `grpc`)
- `headers`: Custom headers for OTEL export (optional)

> **Note**: The `endpoint` setting is automatically configured by the Aspire integration and typically doesn't need to be set manually.

For more information about Data API builder telemetry, see the [official documentation](https://learn.microsoft.com/azure/data-api-builder/concept/monitor/open-telemetry).

## Known Issues

The current imlpementation of the Data API builder .NET Aspire integration does not support HTTPS endpoints. However, this is only a dev-time consideration. Service discovery when published can use HTTPS without any problems.
