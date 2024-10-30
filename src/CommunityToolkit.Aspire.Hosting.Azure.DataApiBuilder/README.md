# CommunityToolkit.Hosting.Azure.DataApiBuilder

## Overview

This .NET Aspire Integration runs [Data API Builder](https://aka.ms/dab/docs) in a container. Data API builder generate secure, feature-rich REST and GraphQL endpoints for Tables, Views and Stored Procedures performing CRUD (Create, Read, Update, Delete, Execute) operations against Azure SQL Database, SQL Server, PostgreSQL, MySQL and Azure CosmosDB. 

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
- `port` - The optional port number for the Data API Builder container. Defaults to `random`.
- `configFilePaths` - Opiotnal paths to the config/schema file(s) for Data API Builder. Default is `./dab-config.json`.

### Data API Builder Container Image Configuration

You can specify a different registry/image/tag by using the `WithImageRegistry`/`WithImage`/`WithImageTag` methods:

```csharp
var dab = builder.AddDataAPIBuilder("dab")
    .WithImageRegistry("mcr.microsoft.com")
    .WithImage("azure-databases/data-api-builder")
    .WithImageTag("latest");
```

## Known Issues

The current imlpementation of Data API Builder does not support HTTPS endpoints.