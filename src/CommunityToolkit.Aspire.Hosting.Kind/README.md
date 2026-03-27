# CommunityToolkit.Aspire.Hosting.Kind

An [Aspire](https://learn.microsoft.com/dotnet/aspire) hosting integration that manages local [Kind](https://kind.sigs.k8s.io/) (Kubernetes in Docker) clusters for development.

## Prerequisites

- **Docker** — Kind runs Kubernetes nodes as Docker containers. Install from [docker.com](https://docs.docker.com/get-docker/).
- **Kind CLI** — The `kind` command must be available on your `PATH`. Install from [kind.sigs.k8s.io](https://kind.sigs.k8s.io/docs/user/quick-start/#installation).

## Getting started

### 1. Install the NuGet package

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Kind
```

### 2. Add a Kind cluster to your AppHost

In your AppHost project (typically `Program.cs`), call `AddKindCluster` on the distributed application builder:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddKindCluster("mycluster");

builder.Build().Run();
```

This creates a Kind cluster named **mycluster** that is provisioned when the AppHost starts and deleted when it shuts down.

## Configuration

### Worker nodes

By default the cluster has a single control-plane node. Add worker nodes with `WithWorkerNodes`:

```csharp
var cluster = builder.AddKindCluster("mycluster")
    .WithWorkerNodes(2);
```

### Kubernetes version

Pin the cluster to a specific Kubernetes version with `WithKubernetesVersion`. When omitted, Kind uses its built-in default.

```csharp
var cluster = builder.AddKindCluster("mycluster")
    .WithKubernetesVersion("v1.32.2");
```

### Cluster lifetime

By default the cluster is deleted when the AppHost shuts down (`ClusterLifetime.Session`). To keep the cluster across AppHost restarts, use `ClusterLifetime.Persistent`:

```csharp
var cluster = builder.AddKindCluster("mycluster")
    .WithClusterLifetime(ClusterLifetime.Persistent);
```

| Value | Behavior |
|---|---|
| `ClusterLifetime.Session` | Cluster is deleted on AppHost shutdown (default). |
| `ClusterLifetime.Persistent` | Cluster survives AppHost restarts and is reused on next startup. |

### Docker networking

Aspire containers run on a separate Docker network from Kind nodes. If you have a container resource that needs to reach the Kind cluster's API server, call `WithKindNetwork` to bridge the two networks:

```csharp
var cluster = builder.AddKindCluster("mycluster");

var worker = builder.AddContainer("my-worker", "myregistry/my-worker")
    .WithReference(cluster)
    .WithKindNetwork();
```

> **Note:** `WithKindNetwork` is available on `IResourceBuilder<ContainerResource>`. It connects the container to the `kind` Docker network automatically when the container starts.

## Connecting services to the cluster

Use `WithReference` to inject Kubernetes connection details into any resource that supports environment variables (`IResourceWithEnvironment`):

```csharp
var cluster = builder.AddKindCluster("mycluster");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(cluster);
```

This sets the following environment variables on the target resource:

| Variable | Description |
|---|---|
| `KUBECONFIG` | Path to the kubeconfig file for the Kind cluster. |
| `K8S_CLUSTER_NAME` | The name of the Kind cluster. |

## Full example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddKindCluster("dev-cluster")
    .WithKubernetesVersion("v1.32.2")
    .WithWorkerNodes(2)
    .WithClusterLifetime(ClusterLifetime.Persistent);

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(cluster);

var deployer = builder.AddContainer("deployer", "bitnami/kubectl")
    .WithReference(cluster)
    .WithKindNetwork();

builder.Build().Run();
```

## Additional information

- [Kind documentation](https://kind.sigs.k8s.io/)
- [.NET Aspire documentation](https://learn.microsoft.com/dotnet/aspire)
- [Aspire Community Toolkit](https://github.com/CommunityToolkit/Aspire)
