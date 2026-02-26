# CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder

## Overview

This Aspire Integration runs [Data API builder](https://aka.ms/dab/docs) in a container. Data API builder generates secure, feature-rich REST and GraphQL endpoints for Tables, Views and Stored Procedures performing CRUD (Create, Read, Update, Delete, Execute) operations against Azure SQL Database, SQL Server, PostgreSQL, MySQL and Azure CosmosDB.

## Usage

### Example 1: Single data source

The docs for a basic configuration file are at [MS Learn](https://learn.microsoft.com/en-us/azure/data-api-builder/configuration/).

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder
    .AddSqlServer("your-server-name")
    .AddDatabase("your-database-name");

var dabConfig = new FileInfo("./dab-config.json");

var dab = builder.AddDataAPIBuilder("dab")
    .WithConfigFile(dabConfig)
    .WithReference(sqlDatabase)
    .WaitFor(sqlDatabase);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

### Example 2: Multiple data sources

The docs for multi-source configuration are at [MS Learn](https://learn.microsoft.com/en-us/azure/data-api-builder/concept/config/multi-data-source). 

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase1 = builder
    .AddSqlServer("your-server-name")
    .AddDatabase("your-database-name");

var sqlDatabase2 = builder
    .AddSqlServer("your-server-name")
    .AddDatabase("your-database-name");

var dabConfig1 = new FileInfo("./dab-config-1.json");
var dabConfig2 = new FileInfo("./dab-config-2.json");

var dab = builder.AddDataAPIBuilder("dab")
    .WithConfigFile(dabConfig1, dabConfig2)
    .WithReference(sqlDatabase1)
    .WithReference(sqlDatabase2)
    .WaitFor(sqlDatabase1)
    .WaitFor(sqlDatabase2);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

> Note: All files are mounted/copied to the same `/App` folder. Each config file must have a unique filename. If a duplicate filename is detected, a friendly `InvalidOperationException` is thrown.

### Example 3: Cosmos DB and a schema file

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cosmosdb = builder
    .AddAzureCosmosDB("myNewCosmosAccountName")
    .AddDatabase("myCosmosDatabaseName");

var dabConfig = new FileInfo("./dab-config.json");
var dabSchema = new FileInfo("./schema.graphql");   

var dab = builder.AddDataAPIBuilder("dab")
    .WithConfigFile(dabConfig, dabSchema)
    .WithReference(cosmosdb)
    .WaitFor(cosmosdb);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

### Example 4: Connection string-only

Sometimes your SQL Server is installed locally or part of your development environment and doesn't need to be created by Aspire. In these cases, you can use the `AddConnectionString` method. This also works for any data source type supported.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder
    .AddConnectionString("your-cs-name");

var dab = builder.AddDataAPIBuilder("dab")
    .WithConfigFile(new FileInfo("./dab-config.json"))
    .WithReference(sqlDatabase);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

### Example 5: Custom image tag

In some cases, including those times when you want to use a release candidate (RC) or a private build of the Data API builder container image, you may want to specify a custom image or image tag. For a custom image tag, you can do this with the `WithImageTag` method, a standard method in Aspire.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder
    .AddConnectionString("your-cs-name");

var dab = builder.AddDataAPIBuilder("dab")
    .WithConfigFile(new FileInfo("./dab-config.json"))
    .WithReference(sqlDatabase)
    .WithImageTag("1.7.86-rc"); // specify a custom image tag

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```


### Example 6: Testing with MCP Inspector

Because version 1.7 and later of Data API builder has MCP capability for agentic applications, you can test and debug your instance with the MCP Inspector which is available through Aspire's CommunityToolkit.Aspire.Hosting.McpInspector version 13.1.1 or later. 


```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sqlDatabase = builder
    .AddConnectionString("your-cs-name");

var dab = builder.AddDataAPIBuilder("dab")
    .WithConfigFile(new FileInfo("./dab-config.json"))
    .WithReference(sqlDatabase)
    .WithImageTag("1.7.86-rc"); // specify a custom image tag

var mcp = builder
    .AddMcpInspector("mcp-inspector", options =>
    {
        options.InspectorVersion = "0.20.0";
    })
    .WithMcpServer(dab, transportType: McpTransportType.StreamableHttp)
    .WithParentRelationship(dab)
    .WithEnvironment("DANGEROUSLY_OMIT_AUTH", "true")
    .WaitFor(dab);

var app = builder
    .AddProject<Projects.Client>()
    .WithReference(dab);

builder.Build().Run();
```

## Toolkit Documentation

The following methods are available for configuring the Data API builder container in Aspire:

| Method | Parameter | Description |
|-|-|-|
|AddDataAPIBuilder() || Adds a Data API builder container to the application.|
|| string name | The name of the resource. |
|| int httpPort | Optional HTTP port number for the Data API builder container. Defaults to a random port. |
| WithConfigFile() || Adds one or more config or schema files to the container.
|| FileInfo[] files | The config or schema file(s) to add. 
| WithConfigFolder() || Adds all files from the specified folder(s) to the container.
|| DirectoryInfo[] folders | The folder(s) from which to add all top-level config or schema files.

### Health Checks

If your Data API builder configuration requires authentication (e.g., EasyAuth, JWT, or any provider other than `Simulator`), the `/health` endpoint may return a non-200 status even when the service is otherwise healthy. In development, consider using the `Simulator` authentication provider in your `dab-config.json` to avoid health check failures:
>
> ```json
> "authentication": {
>   "provider": "Simulator"
> }
> ```

For more information about Data API builder health checks, see the [official documentation](https://learn.microsoft.com/azure/data-api-builder/concept/monitor/health-checks).

For more information about Data API builder's Simulator authentication provider, see the [official documentation](https://learn.microsoft.com/en-us/azure/data-api-builder/concept/security/how-to-authenticate-simulator).

### OpenTelemetry Instrumentation

Aspire automatically injects `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME` into the Data API builder container via `.WithOtlpExporter()`. To enable OTEL telemetry in Data API builder, add the following configuration to your `dab-config.json` file:

```json
{
  "runtime": {
    "telemetry": {
      "open-telemetry": {
        "enabled": true,
        "service-name": "@env('OTEL_SERVICE_NAME')",
        "endpoint": "@env('OTEL_EXPORTER_OTLP_ENDPOINT')",
        "exporter-protocol": "grpc"
      }
    }
  }
}
```

> **Warning:** Do **not** add `"headers": "@env('OTEL_EXPORTER_OTLP_HEADERS')"` unless your OTLP endpoint requires authentication (e.g., a cloud APM service). Aspire does not inject `OTEL_EXPORTER_OTLP_HEADERS`, and DAB requires all `@env()` references to resolve — an unset variable causes a fatal deserialization crash loop. If you need headers, set the environment variable explicitly on the container:
>
> ```csharp
> builder.AddDataAPIBuilder("dab")
>     .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", "api-key=your-key");
> ```

For more information about Data API builder telemetry, see the [official documentation](https://learn.microsoft.com/azure/data-api-builder/concept/monitor/open-telemetry).

