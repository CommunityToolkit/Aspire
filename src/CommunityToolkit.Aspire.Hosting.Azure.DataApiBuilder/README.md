# CommunityToolkit.Hosting.Azure.DataApiBuilder

## Overview

This is a .NET Aspire Integration to run [Data API Builder](https://learn.microsoft.com/azure/data-api-builder/overview) as container. Data API Builder generate REST and GraphQL endpoints performing CRUD (Create, Read, Update, Delete) operations against a database. 

## Usage

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Data API Builder using dab-config.json 
var dab = builder.AddDataAPIBuilder("dab")
    .WithReference(sqlDatabase)
    .WaitFor(sqlServer);

builder.Build().Run();
```

### Configuration

- `name` - The name of the resource.
- `configFilePath` - The path to the config file for Data API Builder. Defaults to `dab-config.json`.
- `port` - The port number for the Data API Builder container. Defaults to `null` so that Aspire can assign a random port.

### Data API Builder Container Image Configuration

The default Data API Builder container image is `mcr.microsoft.com/azure-data-api-builder/azure-data-api-builder:1.2.11`.

You can specify a different registry/image/tag by using the `WithImageRegistry`/`WithImage`/`WithImageTag` methods:

```csharp
var dab = builder.AddDataAPIBuilder("dab")
    .WithImageRegistry("mcr.microsoft.com")
    .WithImage("azure-databases/data-api-builder")
    .WithImageTag("latest");
```


### Database Configuration

In the example we are using a generated password for the database and are not persisting the data. In a production scenario, you probably want to specify the password and persist the data so it does not get lost when the container is restarted.
Here is an example of how you can configure the database:

```csharp
// Add a SQL Server container
var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer("sql", sqlPassword)
    .WithDataVolume("MyDataVolume");

var sqlDatabase = sqlServer.AddDatabase("your-database-name");
```
