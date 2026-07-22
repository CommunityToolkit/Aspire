# CommunityToolkit.Aspire.Hosting.Floci

## Overview

This Aspire integration runs [Floci](https://floci.io) in a container. Floci is a family of high-performance local cloud emulators — `floci/floci` (AWS, 65+ services including Lambda, S3, DynamoDB, SQS, SNS), `floci/floci-az` (Azure — Blob/Queue/Table Storage, Cosmos DB, Functions, Event Hubs, Service Bus), and `floci/floci-gcp` (GCP — Pub/Sub, Firestore, Datastore, Storage, Secret Manager, Cloud Functions) — each API-compatible with its respective cloud.

Every example below is shown in both C# and TypeScript (polyglot AppHost) form.

## Usage

### Example 1: Add an emulator with default configuration

**AWS**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var aws = builder.AddFlociAws("floci-aws");

var api = builder.AddProject<MyApi>("api")
    .WithReference(aws)
    .WaitFor(aws);

builder.Build().Run();
```

```typescript
const builder = await createBuilder();

const aws = await builder.addFlociAws('floci-aws');

const api = await builder.addProject('api', '../MyApi/MyApi.csproj')
    .withReference(aws)
    .waitFor(aws);

await builder.build().run();
```

`WithReference(aws)` / `withReference(aws)` uses the standard Aspire connection string injection and automatically injects the following environment variables into the dependent resource:

| Variable | Value |
|---|---|
| `ConnectionStrings__floci-aws` | `http://localhost:{port}` (standard Aspire connection string) |
| `AWS_ENDPOINT_URL` | `http://localhost:{port}` (host processes) / `http://host.docker.internal:{port}` (containers) |
| `AWS_DEFAULT_REGION` | Region passed to `AddFlociAws`/`addFlociAws` (default: `us-east-1`) |
| `AWS_ACCESS_KEY_ID` | `test` |
| `AWS_SECRET_ACCESS_KEY` | `test` |

**Azure**

```csharp
var azure = builder.AddFlociAzure("floci-az");

builder.AddProject<MyApi>("api")
    .WithReference(azure)
    .WaitFor(azure);
```

```typescript
const azure = await builder.addFlociAzure('floci-az');

await builder.addProject('api', '../MyApi/MyApi.csproj')
    .withReference(azure)
    .waitFor(azure);
```

`WithReference(azure)` / `withReference(azure)` injects the following environment variables into the dependent resource:

| Variable | Value |
|---|---|
| `ConnectionStrings__floci-az` | `http://localhost:{port}` (standard Aspire connection string) |
| `AZURE_STORAGE_CONNECTION_STRING` | Connection string pointed at the Floci Azure endpoint, using the well-known `devstoreaccount1` dev storage account credentials |

**GCP**

```csharp
var gcp = builder.AddFlociGcp("floci-gcp", defaultProjectId: "my-project");

builder.AddProject<MyApi>("api")
    .WithReference(gcp)
    .WaitFor(gcp);
```

```typescript
const gcp = await builder.addFlociGcp('floci-gcp', {
    defaultProjectId: 'my-project',
});

await builder.addProject('api', '../MyApi/MyApi.csproj')
    .withReference(gcp)
    .waitFor(gcp);
```

`WithReference(gcp)` / `withReference(gcp)` injects the following environment variables into the dependent resource:

| Variable | Value |
|---|---|
| `ConnectionStrings__floci-gcp` | `http://localhost:{port}` (standard Aspire connection string) |
| `PUBSUB_EMULATOR_HOST` | `localhost:{port}` (host processes) / `host.docker.internal:{port}` (containers) |
| `FIRESTORE_EMULATOR_HOST` | `localhost:{port}` (host processes) / `host.docker.internal:{port}` (containers) |
| `DATASTORE_EMULATOR_HOST` | `localhost:{port}` (host processes) / `host.docker.internal:{port}` (containers) |
| `STORAGE_EMULATOR_HOST` | `http://localhost:{port}` (host processes) / `http://host.docker.internal:{port}` (containers) |
| `SECRET_MANAGER_EMULATOR_HOST` | `localhost:{port}` (host processes) / `host.docker.internal:{port}` (containers) |
| `GOOGLE_CLOUD_PROJECT` | Project ID passed to `AddFlociGcp`/`addFlociGcp` (default: `floci-local`) |
| `CLOUDSDK_CORE_PROJECT` | Same project ID, for tools that read the `gcloud` CLI's config var instead |

### Example 2: Enable Lambda / Azure Functions / container-backed services

Each emulator needs access to the Docker socket to launch sibling containers for its container-backed services (AWS Lambda, Azure Functions, GCP Cloud Run/Cloud SQL):

```csharp
var aws = builder.AddFlociAws("floci-aws")
    .WithDockerSocket();

var azure = builder.AddFlociAzure("floci-az")
    .WithDockerSocket();

var gcp = builder.AddFlociGcp("floci-gcp")
    .WithDockerSocket();
```

```typescript
const aws = await builder.addFlociAws('floci-aws');
await aws.withDockerSocket();

const azure = await builder.addFlociAzure('floci-az');
await azure.withDockerSocket();

const gcp = await builder.addFlociGcp('floci-gcp');
await gcp.withDockerSocket();
```

On non-standard Docker installations (e.g. Podman, Rancher Desktop), pass the socket path explicitly — this works the same way on all three clouds:

```csharp
var aws = builder.AddFlociAws("floci-aws")
    .WithDockerSocket("/run/user/1000/podman/podman.sock");
```

```typescript
const aws = await builder.addFlociAws('floci-aws');
await aws.withDockerSocket({ socketPath: '/run/user/1000/podman/podman.sock' });
```

### Example 3: Persistent storage

By default each emulator stores all state in memory. Use `WithDataVolume`/`withDataVolume` to persist state across restarts — available on all three clouds:

```csharp
var aws = builder.AddFlociAws("floci-aws")
    .WithDataVolume("floci-data");

var azure = builder.AddFlociAzure("floci-az")
    .WithDataVolume("floci-az-data");

var gcp = builder.AddFlociGcp("floci-gcp")
    .WithDataVolume("floci-gcp-data");
```

```typescript
const aws = await builder.addFlociAws('floci-aws');
await aws.withDataVolume('floci-data');

const azure = await builder.addFlociAzure('floci-az');
await azure.withDataVolume('floci-az-data');

const gcp = await builder.addFlociGcp('floci-gcp');
await gcp.withDataVolume('floci-gcp-data');
```

Or use a host bind mount:

```csharp
var aws = builder.AddFlociAws("floci-aws")
    .WithDataBindMount("/path/to/data");
```

```typescript
const aws = await builder.addFlociAws('floci-aws');
await aws.withDataBindMount('/path/to/data');
```

### Example 4: Custom region/account/project

```csharp
var aws = builder.AddFlociAws("floci-aws",
    defaultRegion: "eu-west-1",
    defaultAccountId: "123456789012");

var gcp = builder.AddFlociGcp("floci-gcp",
    defaultProjectId: "my-project");
```

```typescript
const aws = await builder.addFlociAws('floci-aws', {
    defaultRegion: 'eu-west-1',
    defaultAccountId: '123456789012',
});

const gcp = await builder.addFlociGcp('floci-gcp', {
    defaultProjectId: 'my-project',
});
```

### Example 5: Floci UI web console — single cloud

Run the [Floci UI](https://github.com/floci-io/floci-ui) web console alongside an emulator to browse its hosted resources:

```csharp
var floci = builder.AddFlociAws("floci")
    .WithFlociUI();
```

```typescript
const floci = await builder.addFlociAws('floci');
await floci.withFlociUI();
```

Customize the container name or pin the host port:

```csharp
var floci = builder.AddFlociAws("floci")
    .WithFlociUI(ui => ui.WithHostPort(14500), containerName: "my-floci-ui");
```

```typescript
const floci = await builder.addFlociAws('floci');
await floci.withFlociUI({
    containerName: 'my-floci-ui',
    configureContainer: async (ui) => {
        await ui.withHostPort({ port: 14500 });
    },
});
```

> Note: Floci also has a built-in mechanism to launch the UI as a sidecar container on demand, but that relies on Floci itself talking to the Docker socket and self-discovered endpoints, which does not play well with Aspire's DCP-managed container networking. `WithFlociUI`/`withFlociUI` runs the UI as a first-class Aspire resource instead.

### Example 6: Floci UI web console — all three clouds in one console

A single UI console can attach to any combination of clouds — call `WithFlociUI`/`withFlociUI` on whichever cloud creates the console, then attach the others with `WithPluggedCloud`/`withPluggedCloud`:

```csharp
var aws = builder.AddFlociAws("floci-aws");
var azure = builder.AddFlociAzure("floci-az");
var gcp = builder.AddFlociGcp("floci-gcp");

aws.WithFlociUI(configureContainer: ui =>
{
    ui.WithPluggedCloud(azure);
    ui.WithPluggedCloud(gcp);
});
```

```typescript
const aws = await builder.addFlociAws('floci-aws');
const azure = await builder.addFlociAzure('floci-az');
const gcp = await builder.addFlociGcp('floci-gcp');

await aws.withFlociUI({
    configureContainer: async (ui) => {
        await ui.withPluggedCloudAzure(azure);
        await ui.withPluggedCloudGcp(gcp);
    },
});
```

The UI container (`floci/floci-ui`) is added as a child resource of whichever cloud resource created it, wired to each attached cloud's endpoint over the container network (`FLOCI_ENDPOINT`/`FLOCI_AZURE_ENDPOINT`/`FLOCI_GCP_ENDPOINT`), and is excluded from the deployment manifest (it is a local development tool only).

> Note: In C# `WithPluggedCloud` is a single overloaded method name — the compiler picks the right one from the argument type. In TypeScript there is no overload resolution on the generated bindings, so each cloud gets its own method: `withPluggedCloudAws`, `withPluggedCloudAzure`, `withPluggedCloudGcp`.

### Example 7: Custom Quarkus configuration file (AWS only)

Mount a hand-crafted `application.yml` to tune any Floci setting that does not have an extension method. The file is injected read-only at `/deployments/config/application.yml` — the standard Quarkus Docker config override location.

```csharp
var floci = builder.AddFlociAws("floci")
    .WithConfigFile("./floci.yml");
```

```typescript
const floci = await builder.addFlociAws('floci');
await floci.withConfigFile('./floci.yml');
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

All Floci settings can also be set via `FLOCI_`-prefixed environment variables — `WithConfigFile`/`withConfigFile` is only needed for settings that do not have a dedicated extension method. This is currently only available for the AWS emulator.

### Connection string / endpoint properties

Available on all three cloud resource types:

```csharp
var endpoint = floci.PrimaryEndpoint;
var host = floci.Host;
var port = floci.Port;
var connectionString = floci.ConnectionStringExpression;
```

```typescript
const endpoint = await floci.primaryEndpoint();
const host = await floci.host();
const port = await floci.port();
const connectionString = await floci.connectionStringExpression();
```

`connectionStringExpression` resolves to `http://localhost:{port}` for host processes and `http://host.docker.internal:{port}` for container dependents that call `WithReference`/`withReference`.
