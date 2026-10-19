# CommunityToolkit.Aspire.Hosting.Loki

## Overview

This Aspire integration can be used to include [Grafana Loki](https://grafana.com/oss/loki/) in a container.

## Usage

### Example 1: Add Loki and reference it from an API

In this approach, the API receives Loki's HTTP and gRPC service discovery endpoints and waits for Loki to become healthy.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki");

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Loki_Api>("api")
    .WithReference(loki)
    .WaitFor(loki);

builder.Build().Run();
```

`WithReference(loki)` supplies service discovery endpoints, not a connection string, and does not automatically export application logs. Use `loki.Resource.HttpEndpoint` or `loki.Resource.GrpcEndpoint` when configuring an exporter.

### Example 2: Add Loki with custom configuration and persistent storage

In this approach, Loki uses a custom configuration file, explicit host ports, and a named volume for persistent log storage.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki")
    .WithConfigFile("./loki.yaml")
    .WithHttpHostPort(13100)
    .WithGrpcHostPort(19096)
    .WithDataVolume();

builder.Build().Run();
```

Configuration files are copied to `/etc/loki/local-config.yaml` for local runs and publishing. Keep custom storage paths under `/loki` and listeners on container ports 3100 (HTTP) and 9096 (gRPC). Host ports are randomly assigned unless configured.

Use `.WithDataBindMount("./loki-data")` instead of `.WithDataVolume()` for host storage. The directory must be writable by the container user (UID 10001).

The HTTP listener supports Aspire's `.WithHttpsDeveloperCertificate()`, `.WithHttpsCertificate(certificate)`, and `.WithoutHttpsCertificate()` APIs. During local runs, Aspire uses a development certificate automatically when available and supported. The gRPC listener remains cleartext HTTP/2.

See the [C# and TypeScript examples](https://github.com/CommunityToolkit/Aspire/tree/main/examples/loki) for more usage.