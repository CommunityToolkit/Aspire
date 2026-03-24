# CommunityToolkit.Aspire.Hosting.Kind

A [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) hosting integration for [Kind](https://kind.sigs.k8s.io/) (Kubernetes in Docker) ephemeral clusters.

## Overview

Kind lets you run local Kubernetes clusters using Docker container nodes. This integration enables you to declaratively provision ephemeral Kind clusters as first-class .NET Aspire resources — they are created before your application starts and cleaned up when it stops.

## Prerequisites

- [.NET 8+ SDK](https://dotnet.microsoft.com/download)
- [Aspire 9.x workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or compatible container runtime)
- [`kind` CLI](https://kind.sigs.k8s.io/docs/user/quick-start/#installation) on `PATH`
- [`kubectl`](https://kubernetes.io/docs/tasks/tools/) on `PATH` (for health checks and manifest apply)
- [`helm`](https://helm.sh/docs/intro/install/) on `PATH` (only required if using `WithHelmChart`)

## Usage

### Basic cluster

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddKindCluster("dev-cluster");

builder.Build().Run();
```

The cluster is **created** before any resource starts and **deleted** when the application stops.

### Multi-node cluster with a specific Kubernetes version

```csharp
var cluster = builder
    .AddKindCluster("staging-cluster")
    .WithNodeCount(2)                          // 1 control-plane + 2 workers
    .WithKubernetesVersion("v1.31.0");         // kindest/node:v1.31.0
```

### Port mappings (for ingress controllers)

```csharp
var cluster = builder
    .AddKindCluster("ingress-cluster")
    .WithPortMapping(hostPort: 80, containerPort: 80)
    .WithPortMapping(hostPort: 443, containerPort: 443);
```

### Custom Kind config file

```csharp
var cluster = builder
    .AddKindCluster("custom-cluster")
    .WithConfig("/path/to/kind-config.yaml");  // overrides auto-generated config
```

### Deploy Helm charts and manifests

```csharp
var cluster = builder
    .AddKindCluster("full-cluster")
    .WithManifest("k8s/namespaces.yaml")
    .WithHelmChart("ingress-nginx", "ingress-nginx/ingress-nginx", @namespace: "ingress-nginx");
```

> Manifests are applied before Helm charts (to ensure CRD availability).

### Connection string

The kubeconfig file path is exposed as the resource's connection string, allowing downstream resources to reference it:

```csharp
var cluster = builder.AddKindCluster("dev-cluster");

// Other resources can consume the kubeconfig path via connection string reference
var kubeconfig = cluster.GetEndpoint("default"); // or via connection string expression
```

## API Reference

| Method | Description |
|---|---|
| `AddKindCluster(name)` | Adds a Kind cluster resource |
| `WithNodeCount(n)` | Adds `n` worker nodes (default: 0, control-plane only) |
| `WithKubernetesVersion(v)` | Sets the Kubernetes version (e.g., `"v1.31.0"`) |
| `WithConfig(path)` | Provides a custom Kind config YAML file |
| `WithPortMapping(host, container)` | Maps a host port to a container port |
| `WithHelmChart(release, chart, ns?)` | Installs a Helm chart after cluster readiness |
| `WithManifest(path)` | Applies a kubectl manifest after cluster readiness |
| `WithWaitForReady(timeout)` | Overrides the 5-minute readiness timeout |

## Lifecycle

| Phase | Action |
|---|---|
| `BeforeStart` | `kind create cluster --name {name} [--config {path}] --kubeconfig {path}` |
| `AfterResourcesCreated` | `kubectl get nodes` health check with exponential backoff; then apply manifests + Helm charts |
| `BeforeStop` | `kind delete cluster --name {name}` + kubeconfig file cleanup |

## Security

- Cluster names are validated against `^[a-z0-9][a-z0-9\-]*$` to prevent command injection.
- All CLI arguments are passed as discrete tokens via `ProcessStartInfo.ArgumentList` (never string interpolation).
- stdout and stderr are drained concurrently with `WaitForExitAsync` to prevent OS pipe buffer deadlock.

## Contributing

See the [CommunityToolkit.Aspire contribution guide](https://github.com/CommunityToolkit/Aspire/blob/main/CONTRIBUTING.md).

## License

[MIT](https://github.com/CommunityToolkit/Aspire/blob/main/LICENSE)
