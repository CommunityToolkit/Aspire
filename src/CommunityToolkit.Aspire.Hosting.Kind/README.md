# CommunityToolkit.Aspire.Hosting.Kind

An [Aspire](https://learn.microsoft.com/dotnet/aspire) hosting integration that manages local [Kind](https://kind.sigs.k8s.io/) clusters for development with Docker or Podman, and provides a compute environment for `aspire publish` and `aspire deploy`.

## Prerequisites

- **Docker or Podman** - Kind runs Kubernetes nodes as containers. Install [Docker](https://docs.docker.com/get-docker/) or [Podman](https://podman.io/docs/installation).
- **Kind CLI** - The `kind` command must be available on your `PATH`. Install from [kind.sigs.k8s.io](https://kind.sigs.k8s.io/docs/user/quick-start/#installation).
- **Helm CLI** - Required for deploy scenarios and Helm chart resources. Install from [helm.sh](https://helm.sh/docs/intro/install/).

## Getting started

### 1. Install the NuGet package

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Kind
```

### 2. Add a Kind cluster to your AppHost

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddKindCluster("mycluster");

builder.Build().Run();
```

This creates a Kind cluster named **mycluster** that is provisioned when the AppHost starts and deleted when it shuts down.

## Scenario 1: Kind cluster as a managed dependency (F5 mode)

Use `AddKindCluster` to create a Kind cluster that appears in the Aspire dashboard. Your apps get `KUBECONFIG` and `K8S_CLUSTER_NAME` injected via `WithReference`. Intended for K8s developers building operators, controllers, or admission webhooks.

### Configuration

#### Worker nodes

By default the cluster has a single control-plane node. Add worker nodes with `WithWorkerNodes`:

```csharp
var cluster = builder.AddKindCluster("mycluster")
    .WithWorkerNodes(2);
```

#### Kubernetes version

Pin the cluster to a specific Kubernetes version with `WithKubernetesVersion`. When omitted, Kind uses its built-in default.

```csharp
var cluster = builder.AddKindCluster("mycluster")
    .WithKubernetesVersion("v1.32.2");
```

#### Cluster lifetime

By default the cluster is deleted when the AppHost shuts down (`ClusterLifetime.Session`). To keep the cluster across AppHost restarts, use `ClusterLifetime.Persistent`:

```csharp
var cluster = builder.AddKindCluster("mycluster")
    .WithClusterLifetime(ClusterLifetime.Persistent);
```

| Value | Behavior |
|---|---|
| `ClusterLifetime.Session` | Cluster is deleted on AppHost shutdown (default). |
| `ClusterLifetime.Persistent` | Cluster survives AppHost restarts and is reused on next startup. |

## Networking model

Kind adds an extra network boundary compared to a typical Aspire app, so it helps to think about four separate buckets:

- **Process / executable resources** run on the host machine.
- **Aspire-managed containers** are regular containers created by Aspire on its application container network.
- **Kind cluster** means the Kind control-plane and worker node containers on the runtime's `kind` network.
- **Kind-managed workloads** are Kubernetes workloads running in the Kind cluster, such as Redis installed with `cluster.AddHelmChart(...)`. They run inside the Kind node containers in an isolated network namespace managed by the kindnet CNI, rather than on the host container network. They are reached through Kubernetes networking and exposure mechanisms (NodePort, HostPort, etc.), not as peer Aspire `AddContainer(...)` resources.

In the matrix below, **Yes** means there is a usable network path. **No** means there is no direct path by default.

| From \\ To | Host Process | Aspire-managed container | Kind control plane | Kind-managed workload |
|---|---|---|---|---|
| **Host Process** | Yes | Yes | Yes — Kind publishes the API to host `127.0.0.1:<port>`; `WithReference(kind)` injects kubeconfig | Yes, via `kubectl port-forward` or NodePort on localhost |
| **Aspire-managed container** | Yes | Yes | Yes — `WithReference(kind)` rewrites the kubeconfig and `WithKindNetwork()` joins the runtime's `kind` network | Yes — requires `WithKindNetwork()` plus a Kubernetes exposure mechanism (NodePort, HostPort) |
| **Kind control plane** | No by default | No by default — the control plane has no route to Aspire's container network | Yes | Yes |
| **Kind-managed workload** | No by default | No by default — pods run in an isolated network namespace inside the Kind node container | Yes | Yes |

> `WithKindNetwork()` connects the Aspire container to the runtime's `kind` network, giving it L3 connectivity to the Kind node containers. It does **not** grant access to the Kubernetes pod or service networks — workloads inside the cluster must be exposed via NodePort, HostPort, or a similar mechanism.

### Connecting services to the cluster

#### Container integration

For container resources, `WithReference` provides first-class support that automatically handles all Kind-specific requirements:

```csharp
var cluster = builder.AddKindCluster("mycluster");

var worker = builder.AddContainer("my-worker", "myregistry/my-worker")
    .WithReference(cluster);
```

The container-specific `WithReference` overload automatically:
- Bind-mounts the Kind kubeconfig into the container at `/etc/kubeconfig/config`
- Sets `KUBECONFIG` to the in-container mount path
- Sets `K8S_CLUSTER_NAME` to the Kind cluster name
- Connects the container to the Kind container network

> **Note:** The kubeconfig mounted into containers uses the Kind control-plane container name (e.g., `mycluster-control-plane:6443`) instead of `127.0.0.1`, enabling container-to-container communication over the Kind container network.

### Non-container resources

For non-container resources (e.g., projects, executables), `WithReference` injects environment variables pointing to the host kubeconfig:

```csharp
var cluster = builder.AddKindCluster("mycluster");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(cluster);
```

`WithReference` sets the following environment variables on the target resource:

| Variable | Container resources | Non-container resources |
|---|---|---|
| `KUBECONFIG` | `/etc/kubeconfig/config` inside the container, backed by a bind mount of the container-compatible kubeconfig file. | Path to the host kubeconfig file for the Kind cluster. |
| `K8S_CLUSTER_NAME` | The name of the Kind cluster. | The name of the Kind cluster. |

### Container networking

Aspire containers run on a separate container network from Kind nodes. If you have a container resource that needs to reach the Kind cluster's API server, call `WithKindNetwork` to bridge the two networks:

```csharp
var cluster = builder.AddKindCluster("mycluster");

var worker = builder.AddContainer("my-worker", "myregistry/my-worker")
    .WithReference(cluster)
    .WithKindNetwork();
```

> **Note:** `WithKindNetwork` is available on `IResourceBuilder<ContainerResource>`. It connects the container to the `kind` container network automatically when the container starts.

### Deploying Helm charts to the cluster

Use `AddHelmChart` to deploy pre-built Helm charts to the Kind cluster during F5. Charts are installed when the cluster becomes healthy. They persist with the cluster - deleted with session clusters, retained with persistent clusters.

```csharp
var cluster = builder.AddKindCluster("mycluster")
    .WithKubernetesVersion("v1.32.2");

var redis = cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
    .WithChartVersion("20.0.0")
    .WithHelmValue("replica.replicaCount", "0")
    .WithNamespace("cache");
```

### Full F5 example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddKindCluster("dev-cluster")
    .WithKubernetesVersion("v1.32.2")
    .WithWorkerNodes(2)
    .WithClusterLifetime(ClusterLifetime.Persistent);

// Container automatically gets Kind network + kubeconfig mount
var deployer = builder.AddContainer("deployer", "bitnami/kubectl")
    .WithReference(cluster);

// Non-container resource gets host kubeconfig path
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(cluster);

builder.Build().Run();
```

## Scenario 2: Kind as a compute environment (aspire publish / aspire deploy)

Use `AddKubernetesEnvironment().WithKind()` to enable `aspire publish` (generates Helm charts) and `aspire deploy` (creates cluster + deploys). Designed for K8s consumers who want to test their Helm charts locally before deploying to a real cluster like AKS.

```csharp
#pragma warning disable ASPIREPIPELINES001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddKubernetesEnvironment("k8s")
    .WithKind()
    .WithKubernetesVersion("v1.32.2");

builder.AddContainer("redis", "redis", "7");
builder.AddProject<Projects.MyApi>("api");

builder.Build().Run();
```

Then from the command line:

```bash
# Generate Helm chart
aspire publish --output-path ./charts

# Or deploy directly to Kind
aspire deploy
```

### How deploy works

When you run `aspire deploy`, the pipeline executes these steps in order:

1. **publish** - Generates Kubernetes manifests as a Helm chart via `Aspire.Hosting.Kubernetes`
2. **kind-create-cluster** - Creates the Kind cluster (reuses existing if persistent)
3. **build** - Builds container images for project resources (`dotnet publish /t:PublishContainer`)
4. **kind-load-images** - Loads all container images into Kind (`kind load docker-image`)
5. **kind-helm-install** - Installs the generated Helm chart (`helm install`)

## Advanced usage

### Manual Kind network connection

The container-specific `WithReference` automatically connects containers to the Kind network. If you need to manually control network connectivity (e.g., for containers that don't reference the cluster), you can use `WithKindNetwork` explicitly:

```csharp
builder.AddContainer("my-container", "my-image")
    .WithKindNetwork();
```

## API reference

| Method | Description |
|--------|-------------|
| `AddKindCluster(name)` | Adds a Kind cluster resource visible in the dashboard (scenario 1) |
| `WithKubernetesVersion(string)` | Sets the Kubernetes version (e.g., `"v1.32.2"`) |
| `WithWorkerNodes(int)` | Sets the number of worker nodes (default: 0, control-plane only) |
| `WithClusterLifetime(ClusterLifetime)` | `Session` (default) or `Persistent` |
| `WithReference(kind)` | Injects `KUBECONFIG` and `K8S_CLUSTER_NAME` into another resource |
| `WithKindNetwork()` | Connects a container to the Kind container network |
| `AddHelmChart(name, chartRef)` | Deploys a Helm chart to the Kind cluster during F5 |
| `WithKind()` | Configures a `KubernetesEnvironmentResource` to deploy to a local Kind cluster (scenario 2) |
## Additional information

- [Kind documentation](https://kind.sigs.k8s.io/)
- [.NET Aspire documentation](https://learn.microsoft.com/dotnet/aspire)
- [Aspire Community Toolkit](https://github.com/CommunityToolkit/Aspire)
