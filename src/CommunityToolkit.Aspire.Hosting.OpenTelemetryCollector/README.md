# CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector

## Overview

This .NET Aspire Integration can be used to include [OpenTelemetry Collector](https://github.com/open-telemetry/opentelemetry-collector) in a container.

## Usage

### Example 1: Add OpenTelemetry Collector without automatic redirection

In this approach, only the projects and resource that you forward the collector to will have their telemetry forwarded to the collector.

```csharp
var builder = DistributedApplication.CreateBuilder(args);


var collector = builder.AddOpenTelemetryCollector("opentelemetry-collector")
    .WithConfig("./config.yaml");

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_OpenTelemetryCollector_Api>("api")
    .WithOpenTelemetryCollectorRouting(collector);

builder.Build().Run();
```

### Example 2: Add OpenTelemetry Collector with automatic redirection

In this approach, all projects and resources that have the `OtlpExporterAnnotation` will have their telemetry forwarded to the collector.

```csharp
var builder = DistributedApplication.CreateBuilder(args);


var collector = builder.AddOpenTelemetryCollector("opentelemetry-collector")
    .WithConfig("./config.yaml")
    .WithAppForwarding();

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_OpenTelemetryCollector_Api>("api");

builder.Build().Run();
```
