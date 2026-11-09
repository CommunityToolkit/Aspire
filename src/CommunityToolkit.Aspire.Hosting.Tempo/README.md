# Tempo hosting integration

Use this integration to model, configure, and orchestrate [Grafana Tempo](https://grafana.com/oss/tempo/) as an Aspire container resource with OTLP trace ingestion.

## Getting started

```shell
aspire add CommunityToolkit.Aspire.Hosting.Tempo
```

## Usage example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var tempo = builder.AddTempo("tempo");

builder.AddProject<Projects.Api>("api")
    .WithReference(tempo)
    .WaitFor(tempo);

builder.Build().Run();
```

`WithReference(tempo)` supplies service discovery endpoints, not a connection string, and does not automatically export traces. Use `tempo.Resource.OtlpGrpcEndpoint` or `tempo.Resource.OtlpHttpEndpoint` to configure an exporter. `GrpcEndpoint` is Tempo's internal gRPC listener, not an OTLP receiver. `HttpEndpoint` serves trace queries and readiness.

### Configuration and storage

```csharp
var tempo = builder.AddTempo("tempo")
    .WithConfigFile("./tempo.yaml")
    .WithHttpHostPort(13200)
    .WithGrpcHostPort(19095)
    .WithOtlpGrpcHostPort(14317)
    .WithOtlpHttpHostPort(14318)
    .WithDataVolume();
```

Default configuration enables OTLP gRPC on container port 4317, OTLP HTTP on 4318, HTTP queries on 3200, and internal gRPC on 9095. Host ports are random unless configured. HTTP listeners use HTTP and gRPC listeners use cleartext HTTP/2. Unlike Loki, this integration does not configure Aspire-managed HTTPS certificates.

Configuration files are copied to `/etc/tempo/tempo.yaml` for local runs and publishing. Custom configurations must retain these listener ports, bind OTLP receivers to `0.0.0.0`, and use storage paths under `/var/tempo` when combined with data mount helpers. The image owns this directory; `/tempo` is the server executable, not a storage directory. Local storage is intended for development, not production deployments.

Storage is ephemeral unless explicitly configured. Use `.WithDataBindMount("./tempo-data")` instead of `.WithDataVolume()` for host storage. The directory must be writable by the container user (UID 10001). Container lifetime is independent of storage; opt into `.WithLifetime(ContainerLifetime.Persistent)` when needed.

Tempo does not provide built-in authentication. Protect deployed endpoints with appropriate network controls or an authenticating proxy.