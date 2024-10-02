# CommunityToolkit.Hosting.Azure.StaticWebApps

## Overview

This is a .NET Aspire Integration to run [Data API Builder](https://learn.microsoft.com/en-us/azure/data-api-builder/overview) as container. Data API Builder generate REST and GraphQL endpoints performing CRUD (Create, Read, Update, Delete) operations against a database. 

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
- `containerRegistry` - The container registry for the Data API Builder image. Defaults to `mcr.microsoft.com`.
- `containerImageName` - The name of the Data API Builder image. Defaults to `azure-data-api-builder`.
- `containerImageTag` - The tag of the Data API Builder image. Defaults to `latest`.
- `port` - The port number for the Data API Builder container. Defaults to `5000`.
- `targetPort` - The target port number for the Data API Builder container. Defaults to `5000`.
