# CommunityToolkit.Aspire.Hosting.Loki

## Overview

This Aspire integration can be used to include [Grafana Loki](https://grafana.com/oss/loki/) in an Aspire AppHost.

## Usage

### Example 1: Add Loki and reference it from an API

In this approach, the API receives Loki's HTTP connection properties and waits for Loki to become healthy.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki");

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Loki_Api>("api")
    .WithReference(loki)
    .WaitFor(loki);

builder.Build().Run();
```

`WithReference(loki)` supplies `ConnectionStrings__loki` with Loki's HTTP base URI. Configure your application's logging exporter separately; referencing Loki does not automatically export application logs.

### Example 2: Configure Loki image, ports, and persistent storage

In this approach, Loki uses explicit host ports and a named volume for persistent log storage.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki", settings =>
{
    settings.HttpPort = 13100;
    settings.GrpcPort = 19096;
})
    .WithDataVolume();

builder.Build().Run();
```

Loki listens on container ports 3100 for HTTP and 9096 for gRPC. Readiness is checked through `/ready`. Use `.WithDataBindMount("./loki-data")` for a host directory or `.WithConfigBindMount("./loki.yaml")` for custom configuration.