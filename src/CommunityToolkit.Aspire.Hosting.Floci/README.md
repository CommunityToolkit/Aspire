# CommunityToolkit.Aspire.Hosting.Floci

## Overview

This Aspire integration runs [Floci](https://floci.io) in a container. Floci is a high-performance AWS emulator (65+ services including Lambda, S3, DynamoDB, SQS, SNS, and more) that is API-compatible with LocalStack.

## Usage

### Example 1: Add Floci with default configuration

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var floci = builder.AddFloci("floci");

var api = builder.AddProject<MyApi>("api")
    .WithReference(floci)
    .WaitFor(floci);

builder.Build().Run();
```

`WithReference(floci)` uses the standard Aspire connection string injection and automatically injects the following environment variables into the dependent resource:

| Variable | Value |
|---|---|
| `ConnectionStrings__floci` | `http://localhost:{port}` (standard Aspire connection string) |
| `AWS_ENDPOINT_URL` | `http://localhost:{port}` (host processes) / `http://host.docker.internal:{port}` (containers) |
| `AWS_DEFAULT_REGION` | Region passed to `AddFloci` (default: `us-east-1`) |
| `AWS_ACCESS_KEY_ID` | `test` |
| `AWS_SECRET_ACCESS_KEY` | `test` |

### Example 2: Enable Lambda / container-backed services

Floci requires access to the Docker socket to launch sibling containers for Lambda and other container-backed services:

```csharp
var floci = builder.AddFloci("floci")
    .WithDockerSocket();
```

On non-standard Docker installations (e.g. Podman, Rancher Desktop), pass the socket path explicitly:

```csharp
var floci = builder.AddFloci("floci")
    .WithDockerSocket("/run/user/1000/podman/podman.sock");
```

### Example 3: Persistent storage

By default Floci stores all state in memory. Use `WithDataVolume` to persist state across restarts:

```csharp
var floci = builder.AddFloci("floci")
    .WithDataVolume("floci-data");
```

Or use a host bind mount:

```csharp
var floci = builder.AddFloci("floci")
    .WithDataBindMount("/path/to/data");
```

### Example 4: Custom region and account

```csharp
var floci = builder.AddFloci("floci",
    defaultRegion: "eu-west-1",
    defaultAccountId: "123456789012");
```

### Example 5: Floci UI web console

Run the [Floci UI](https://github.com/floci-io/floci-ui) web console alongside the emulator to browse the hosted AWS resources:

```csharp
var floci = builder.AddFloci("floci")
    .WithFlociUI();
```

The UI container (`floci/floci-ui`) is added as a child resource of the Floci container, automatically wired to the Floci endpoint over the container network via `FLOCI_ENDPOINT`, inherits the region and account ID from `AddFloci`, and is excluded from the deployment manifest (it is a local development tool only).

Customize the container name or pin the host port:

```csharp
var floci = builder.AddFloci("floci")
    .WithFlociUI(ui => ui.WithHostPort(14500), containerName: "my-floci-ui");
```

> Note: Floci also has a built-in mechanism to launch the UI as a sidecar container on demand, but that relies on Floci itself talking to the Docker socket and self-discovered endpoints, which does not play well with Aspire's DCP-managed container networking. `WithFlociUI` runs the UI as a first-class Aspire resource instead.

### Example 6: Custom Quarkus configuration file

Mount a hand-crafted `application.yml` to tune any Floci setting that does not have an extension method. The file is injected read-only at `/deployments/config/application.yml` — the standard Quarkus Docker config override location.

```csharp
var floci = builder.AddFloci("floci")
    .WithConfigFile("./floci.yml");
```

A minimal `floci.yml` that enables debug logging and disables signature validation:

```yaml
floci:
  auth:
    validate-signatures: false
quarkus:
  log:
    level: DEBUG
```

All Floci settings can also be set via `FLOCI_`-prefixed environment variables — `WithConfigFile` is only needed for settings that do not have a dedicated extension method.

### Example 7: TLS / HTTPS

**Aspire development certificate**

Chain the standard Aspire `WithHttpsDeveloperCertificate()` API directly on the `AddFloci` return value. `AddFloci` registers a `WithHttpsCertificateConfiguration` callback internally that fires only when a certificate is configured and automatically sets `FLOCI_TLS_ENABLED`, `FLOCI_TLS_CERT_PATH`, and `FLOCI_TLS_KEY_PATH`. No integration-specific wrapper method is needed.

```csharp
var floci = builder.AddFloci("floci")
    .WithHttpsDeveloperCertificate();
```

When TLS is enabled the `ConnectionStringExpression` and the injected `AWS_ENDPOINT_URL` automatically switch to the `https://` scheme. Both HTTP and HTTPS connect to the same port (4566); Floci's TLS proxy handles routing based on the connection protocol. Run `aspire certs trust` once to add the Aspire dev certificate to your system trust store so clients skip certificate verification errors.

**Bring-your-own PEM certificate**

Use the standard Aspire `WithHttpsCertificate(cert)` API instead. The same internal callback fires and wires up the Floci TLS environment variables from the container-side PEM paths.

```csharp
var cert = X509Certificate2.CreateFromPemFile("/certs/floci.crt", "/certs/floci.key");

var floci = builder.AddFloci("floci")
    .WithHttpsCertificate(cert);
```
