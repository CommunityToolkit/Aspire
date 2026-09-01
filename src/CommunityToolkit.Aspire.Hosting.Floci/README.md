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
    .withFlociAwsReference(aws)
    .waitFor(aws);

await builder.build().run();
```

`WithReference(aws)` / `withFlociAwsReference(aws)` uses the standard Aspire connection string injection and additionally injects the environment variables the AWS SDKs already read, so no SDK configuration is needed in the dependent:

| Variable | Value |
|---|---|
| `ConnectionStrings__floci-aws` | `http://localhost:{port}` (standard Aspire connection string) |
| `AWS_ENDPOINT_URL` | The emulator endpoint, resolved by Aspire per dependent (see [Endpoint resolution](#endpoint-resolution)) |
| `AWS_DEFAULT_REGION` | Region passed to `AddFlociAws`/`addFlociAws` (default: `us-east-1`) |
| `AWS_ACCESS_KEY_ID` | `test` |
| `AWS_SECRET_ACCESS_KEY` | `test` |

> Note: In C# each cloud contributes a `WithReference` overload, so `WithReference(aws)` is picked by argument type. The generated TypeScript bindings have no overload resolution, so each cloud gets its own method: `withFlociAwsReference`, `withFlociAzureReference`, `withFlociGcpReference`.

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
    .withFlociAzureReference(azure)
    .waitFor(azure);
```

`WithReference(azure)` / `withFlociAzureReference(azure)` injects the following environment variables into the dependent resource:

| Variable | Value |
|---|---|
| `ConnectionStrings__floci-az` | `http://localhost:{port}` (standard Aspire connection string) |
| `AZURE_STORAGE_CONNECTION_STRING` | Development storage connection string pointed at the Floci Azure endpoint, carrying `BlobEndpoint`, `QueueEndpoint` and `TableEndpoint` and the well-known `devstoreaccount1` dev credentials |

For **Cosmos DB**, use `WithCosmos()` / `withCosmos()` to model the Cosmos API as a child resource, then reference it through Aspire's standard connection-string flow:

```csharp
var azure = builder.AddFlociAzure("floci-az");
var cosmos = azure.WithCosmos();

builder.AddProject<MyApi>("api")
    .WithReference(azure)    // storage variables (optional)
    .WithReference(cosmos)   // ConnectionStrings__cosmos
    .WaitFor(azure);
```

```typescript
const azure = await builder.addFlociAzure('floci-az');
const cosmos = await azure.withCosmos();

await builder.addProject('api', '../MyApi/MyApi.csproj')
    .withFlociAzureReference(azure)
    .withReference(cosmos)
    .waitFor(azure);
```

App side, this is the standard Aspire flow:

```csharp
builder.AddAzureCosmosClient("cosmos");
```

| Variable | Value |
|---|---|
| `ConnectionStrings__{resourceName}` (default `cosmos`) | `AccountEndpoint={scheme}://{host}:{port}/{account}-cosmos/;AccountKey=…` — the well-known Cosmos DB emulator key. Resource name and account name (default `devstoreaccount1`) are configurable. |

The Cosmos child resource is additive, so combine `WithReference(cosmos)` with `WithReference(azure)` when you also want the base endpoint / storage variables. (Talking to the floci Cosmos emulator over HTTP from the .NET SDK still needs the usual client-side settings — Gateway mode, and HTTP/1.1 — which are the app's concern, as with any local Cosmos emulator.)

For **Service Bus**, use `WithServiceBus()` / `withServiceBus()` to model the AMQP data plane as a child resource, then reference it through Aspire's standard connection-string flow:

```csharp
var azure = builder.AddFlociAzure("floci-az")
    .WithDockerSocket();
var serviceBus = azure.WithServiceBus();

builder.AddProject<MyApi>("api")
    .WithReference(serviceBus) // ConnectionStrings__servicebus
    .WaitFor(azure);
```

```typescript
const azure = (await builder.addFlociAzure('floci-az')).withDockerSocket();
const serviceBus = await azure.withServiceBus();

await builder.addProject('api', '../MyApi/MyApi.csproj')
    .withReference(serviceBus)
    .waitFor(azure);
```

App side, this is the standard Aspire flow:

```csharp
builder.AddAzureServiceBusClient("servicebus");
```

| Variable | Value |
|---|---|
| `ConnectionStrings__{resourceName}` (default `servicebus`) | `Endpoint=sb://localhost:{amqpPort};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;` — the official Service Bus emulator's connection-string shape |

`WithServiceBus` sets `FLOCI_AZ_SERVICES_SERVICE_BUS_MOCKED=false` and `FLOCI_AZ_SERVICES_SERVICE_BUS_START_ON_BOOT=true`. Aspire models the sidecar's AMQP and AMQPS host ports as proxyless endpoints and allocates them by default; pass `amqpPort` / `amqpTlsPort` to use fixed ports. The management plane (for example, `ServiceBusAdministrationClient`) remains on the base endpoint from `WithReference(azure)`. Requires `WithDockerSocket()` and floci-az 0.12.0 or later.

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
    .withFlociGcpReference(gcp)
    .waitFor(gcp);
```

`WithReference(gcp)` / `withFlociGcpReference(gcp)` injects the following environment variables into the dependent resource:

| Variable | Value |
|---|---|
| `ConnectionStrings__floci-gcp` | `http://localhost:{port}` (standard Aspire connection string) |
| `PUBSUB_EMULATOR_HOST` | `{host}:{port}` (see [Endpoint resolution](#endpoint-resolution)) |
| `FIRESTORE_EMULATOR_HOST` | `{host}:{port}` |
| `DATASTORE_EMULATOR_HOST` | `{host}:{port}` |
| `STORAGE_EMULATOR_HOST` | `http://{host}:{port}` — the Storage SDK expects a full URL here, unlike the others |
| `SECRET_MANAGER_EMULATOR_HOST` | `{host}:{port}` |
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

A single UI console can attach to any combination of clouds — call `WithFlociUI`/`withFlociUI` on whichever cloud creates the console, then attach the others with `WithReference`/`withCloudReference*`:

```csharp
var aws = builder.AddFlociAws("floci-aws");
var azure = builder.AddFlociAzure("floci-az");
var gcp = builder.AddFlociGcp("floci-gcp");

aws.WithFlociUI(configureContainer: ui =>
{
    ui.WithReference(azure);
    ui.WithReference(gcp);
});
```

```typescript
const aws = await builder.addFlociAws('floci-aws');
const azure = await builder.addFlociAzure('floci-az');
const gcp = await builder.addFlociGcp('floci-gcp');

await aws.withFlociUI({
    configureContainer: async (ui) => {
        await ui.withFlociAzureReference(azure);
        await ui.withFlociGcpReference(gcp);
    },
});
```

The UI container (`floci/floci-ui`) is added as a child resource of whichever cloud resource created it, wired to each attached cloud's endpoint over the container network (`FLOCI_ENDPOINT`/`FLOCI_AZURE_ENDPOINT`/`FLOCI_GCP_ENDPOINT`), and is excluded from the deployment manifest (it is a local development tool only).

> Note: In C# these are `WithReference` overloads on the UI resource builder — the compiler picks the right one from the argument type. In TypeScript there is no overload resolution on the generated bindings, so each cloud gets its own method: `withFlociAwsReference`, `withFlociAzureReference`, `withFlociGcpReference`.

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

### Example 8: TLS / HTTPS (AWS and Azure)

Both images serve HTTP **and** HTTPS on the *same* port, so enabling TLS never changes the port — only the scheme handed to dependents.

The integration hooks Aspire's own certificate plumbing, so the idiomatic Aspire APIs just work: configure a certificate and the emulator picks it up. Nothing Floci-specific to call.

```csharp
var aws = builder.AddFlociAws("floci-aws")
    .WithHttpsDeveloperCertificate();     // Aspire API — provisions and mounts the key pair

var azure = builder.AddFlociAzure("floci-az")
    .WithHttpsDeveloperCertificate();

builder.AddProject<MyApi>("api")
    .WithReference(aws)     // AWS_ENDPOINT_URL                = https://...
    .WithReference(azure);  // AZURE_STORAGE_CONNECTION_STRING = DefaultEndpointsProtocol=https;...
```

```typescript
const aws = await builder.addFlociAws('floci-aws');
await aws.withHttpsDeveloperCertificate();
```

Under the hood the integration registers a `WithHttpsCertificateConfiguration` callback that maps Aspire's provisioned paths onto the image's own settings, and switches the primary endpoint to `https` before start:

| Aspire-provided value | AWS | Azure |
|---|---|---|
| — | `FLOCI_TLS_ENABLED=true` | `FLOCI_AZ_TLS_ENABLED=true` |
| — | `FLOCI_TLS_SELF_SIGNED=false` | `FLOCI_AZ_TLS_SELF_SIGNED=false` |
| `context.CertificatePath` | `FLOCI_TLS_CERT_PATH` | `FLOCI_AZ_TLS_CERT_PATH` |
| `context.KeyPath` | `FLOCI_TLS_KEY_PATH` | `FLOCI_AZ_TLS_KEY_PATH` |

Because the ASP.NET Core development certificate is already in your machine's trust store, host-process dependents validate it with no extra client configuration. For container dependents, add Aspire's `WithDeveloperCertificateTrust(true)` to install the trust bundle. Any other Aspire certificate source (`WithHttpsCertificate(...)`, `WithCertificatesFromFile`, `WithCertificatesFromStore`) is honoured the same way.

Reach for this when a client refuses plain HTTP — the Cosmos DB Java SDK, or the `azurerm` Terraform/OpenTofu provider, which discovers the cloud over `https://<host>/metadata/endpoints`.

> Plain HTTP stays the default. Unlike HTTPS-first resources, merely having a trusted development certificate on the machine does **not** flip an existing AppHost to `https` — a certificate has to be asked for explicitly.
>
> The [Floci UI](#example-5-floci-ui-web-console--single-cloud) console keeps using the emulator's plain-HTTP listener even when TLS is on. It reaches the emulator by container-network name, which neither the development certificate (SAN `localhost` only) nor a host-issued certificate covers, so HTTPS there would fail hostname validation regardless of trust. Since both protocols share the port, the console connects normally.
>
> The GCP emulator (`floci/floci-gcp`) has no HTTPS listener, so no certificate callback is registered for it and a configured certificate has no effect there.

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

`connectionStringExpression` is an unresolved endpoint expression — see below.

### Endpoint resolution

Every environment variable this integration injects carries an Aspire endpoint expression rather than a literal address, so Aspire resolves it against the network the *dependent* is on:

| Dependent | Resolves to |
|---|---|
| Project / executable (host process) | `localhost:{hostPort}` |
| Sibling container | `{flociResourceName}:{targetPort}` on the container network |

Nothing is hard-coded to `host.docker.internal`, so this works on Docker Desktop, plain Linux Docker, Podman and Rancher Desktop alike.

The scheme is `http` unless [a certificate has been configured](#example-8-tls--https-aws-and-azure), in which case it becomes `https` on the same port.
