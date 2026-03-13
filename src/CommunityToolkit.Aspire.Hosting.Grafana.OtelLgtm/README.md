# CommunityToolkit.Aspire.Hosting.Grafana.OtelLgtm

## Overview

This Aspire integration adds the [Grafana OTel-LGTM](https://github.com/grafana/docker-otel-lgtm) observability stack as a container resource. The `grafana/otel-lgtm` Docker image bundles the **OpenTelemetry Collector**, **Prometheus** (metrics), **Loki** (logs), **Tempo** (traces), **Pyroscope** (profiles), and **Grafana** into a single container — ideal for development, demo, and testing environments.

> **Note**: This image is intended for non-production use. For production monitoring, see [Grafana Cloud Application Observability](https://grafana.com/products/cloud/application-observability/).

## Installation

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Grafana.OtelLgtm
```

## Usage

### Example 1: Add Grafana OTel-LGTM with automatic telemetry forwarding

All resources with OpenTelemetry exporters will automatically send telemetry to the LGTM stack.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
    .WithCollectorConfig("./otelcol-config.yaml")
    .WithAppForwarding();

builder.AddProject<Projects.MyApi>("api");

builder.Build().Run();
```

### Example 2: Add with a fixed Grafana port and data persistence

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm", grafanaPort: 3000)
    .WithCollectorConfig("./otelcol-config.yaml")
    .WithDataVolume()
    .WithAppForwarding();

builder.AddProject<Projects.MyApi>("api");

builder.Build().Run();
```

### Example 3: Custom Grafana and Prometheus configuration

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm")
    .WithCollectorConfig("./otelcol-config.yaml")
    .WithGrafanaConfig("./custom.ini")
    .WithPrometheusConfig("./prometheus.yaml")
    .WithAppForwarding();

builder.Build().Run();
```

### Example 4: Configure settings (image, endpoints)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var lgtm = builder.AddGrafanaOtelLgtm("grafana-lgtm", configureSettings: settings =>
{
    settings.Tag = "0.21.0";
    settings.EnableGrpcEndpoint = true;
    settings.EnableHttpEndpoint = false;
    settings.ForceNonSecureReceiver = true;
})
    .WithAppForwarding();

builder.Build().Run();
```

## Endpoints

| Endpoint | Port | Description |
|----------|------|-------------|
| Grafana UI | 3000 | Web interface for dashboards, explore, and alerting |
| OTLP gRPC | 4317 | OpenTelemetry Collector gRPC receiver |
| OTLP HTTP | 4318 | OpenTelemetry Collector HTTP receiver |
| Prometheus | 9090 | Prometheus metrics query API |
| Pyroscope | 4040 | Pyroscope continuous profiling API |

## Image Versioning

This integration uses the `grafana/otel-lgtm:0.21.0` image tag by default. The image is published to [Docker Hub](https://hub.docker.com/r/grafana/otel-lgtm).
