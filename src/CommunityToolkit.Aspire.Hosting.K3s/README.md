# CommunityToolkit.Aspire.Hosting.K3s

Provides extension methods and resource definitions for the .NET Aspire AppHost to run a
[k3s](https://k3s.io/) lightweight Kubernetes cluster as part of the local development
inner loop. The cluster, Helm chart installs, manifest applies, and service endpoint
exposures all appear as first-class resources in the Aspire dashboard — no external
tooling beyond a supported container runtime is required.

## Getting Started

### Prerequisites

- A container runtime that supports privileged Linux containers:
  - **Docker Engine 20.10+** (Linux) or **Docker Desktop** (macOS / Windows)
  - **Podman 4.0+** (Linux) — works with rootful Podman; rootless requires cgroup v2 delegation

> **Note:** k3s requires `--privileged`, `--cgroupns=host`, and a writable cgroup
> filesystem mount. These flags are passed automatically by the integration. Whether your
> runtime honours them depends on your system configuration.

### Install the package

In your AppHost project:

```sh
dotnet add package CommunityToolkit.Aspire.Hosting.K3s
```

### Quick start

```csharp
var cluster = builder.AddK3sCluster("k8s");

// Inject kubeconfig into a project — KUBECONFIG env var points to local/kubeconfig.yaml
builder.AddProject<Projects.MyOperator>("operator")
    .WaitFor(cluster)
    .WithReference(cluster);
```

## Deploying Helm charts

`AddHelmRelease` runs `helm upgrade --install` inside an `alpine/helm` container on the
DCP network. No host-side `helm` binary is required. The container exits with code 0 on
success, so use `WaitForCompletion` on any resource that depends on the chart being installed.

```csharp
var argocd = cluster.AddHelmRelease("argocd", "argo-cd",
    repo: "https://argoproj.github.io/argo-helm",
    version: "7.8.0",
    @namespace: "argocd")
    .WithHelmValue("server.insecure", "true")
    .WithHelmValuesFile("./deploy/argocd-values.yaml");

builder.AddProject<Projects.MyApi>("api")
    .WaitForCompletion(argocd)   // wait for the chart install to finish
    .WithReference(cluster);
```

### Helm override precedence

Values are applied in this order (last wins):

1. `WithHelmValuesFile` calls — in the order they are declared (`0-`, `1-`, … prefix)
2. `WithHelmValue` `--set` flags — always override values files

## Applying Kubernetes manifests

`AddK8sManifest` runs `kubectl apply --server-side` inside an `rancher/kubectl` container.
No host-side `kubectl` binary is required. Kustomize overlays are auto-detected.

```csharp
// Plain YAML file or directory
var crd = cluster.AddK8sManifest("widget-crd", "./k8s/crds/");

// Kustomize overlay — detected automatically when kustomization.yaml is present
var overlay = cluster.AddK8sManifest("prod-overlay", "./k8s/overlays/local");

// Gate dependent resources until the CRD is Established
builder.AddProject<Projects.WidgetOperator>("operator")
    .WaitForCompletion(crd)
    .WithReference(cluster);
```

For Kustomize overlays that reference base directories outside the overlay path, point
`AddK8sManifest` to the common root and specify the overlay path in `kustomization.yaml`.

## Exposing k8s services to the Aspire network

`AddServiceEndpoint` starts an in-process KubernetesClient WebSocket port-forward bound
to `0.0.0.0:{allocatedPort}`. No NodePort configuration is required.

```csharp
var ui = cluster.AddServiceEndpoint("argocd-ui",
    serviceName: "argocd-server",
    servicePort: 443,
    @namespace: "argocd")
    .WaitForCompletion(argocd);    // wait for chart install before port-forwarding

// Host processes receive services__argocd-ui__url=https://localhost:{port}
builder.AddProject<Projects.Consumer>("consumer")
    .WaitFor(ui)
    .WithReference(ui);

// DCP containers receive https://host.docker.internal:{port}
// --add-host=host.docker.internal:host-gateway is injected automatically on Linux
builder.AddContainer("sidecar", "myorg/sidecar")
    .WaitFor(ui)
    .WithReference(ui);
```

## Kubeconfig injection

`WithReference(cluster)` selects the injection mode automatically:

| Consumer type | What is injected |
|---|---|
| `ProjectResource` / `ExecutableResource` | `KUBECONFIG=…/.k3s/k8s/local/kubeconfig.yaml` |
| `ContainerResource` | Bind-mount of `container/kubeconfig.yaml` at `/var/k3s/` + `KUBECONFIG=/var/k3s/kubeconfig.yaml` |

All standard Kubernetes tooling (`kubectl`, `helm`, KubernetesClient SDK) reads `KUBECONFIG` automatically — no custom bootstrap code required.

Reading in .NET:
```csharp
// Works identically for both projects and containers — the SDK reads KUBECONFIG automatically.
var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
    Environment.GetEnvironmentVariable("KUBECONFIG"));
using var client = new Kubernetes(config);
```

## Persistent cluster state

```csharp
builder.AddK3sCluster("k8s")
    .WithDataVolume()          // persists /var/lib/rancher/k3s across AppHost restarts
    .WithLifetime(ContainerLifetime.Persistent);
```

Without `WithDataVolume` the cluster state is ephemeral — each AppHost start produces a
fresh cluster. With it, subsequent starts reuse the existing cluster and skip
reinitialisation, making startup significantly faster.

## Multi-node clusters

```csharp
builder.AddK3sCluster("k8s", configure: opts =>
{
    opts.AgentCount = 2;    // 1 server + 2 agents
});
```

The health check waits for all nodes to reach `Ready` before the cluster is marked healthy.

## Image overrides

The `alpine/helm` and `rancher/kubectl` images are pinned but configurable:

```csharp
builder.AddK3sCluster("k8s", configure: opts =>
{
    opts.HelmImage   = "my-registry/helm";
    opts.HelmTag     = "3.18.0";
    opts.KubectlImage = "my-registry/k8s";
    opts.KubectlTag   = "1.33.0";
});
```

## Reaching Aspire services from k3s pods

k3s pods run on the internal pod network (`10.42.0.0/16`). k3s's Flannel CNI masquerades
pod traffic through the k3s container's DCP network IP, so pods can reach DCP services by
their host-mapped port — but not by their Aspire DNS name. Use Helm values or a ConfigMap
to inject the host-accessible address:

```csharp
var postgres = builder.AddPostgres("db");

// Resolve the host-mapped port at configuration time
var dbPort = postgres.GetEndpoint("tcp");

cluster.AddHelmRelease("my-operator", "my-operator-chart")
    .WithHelmValue("database.host", "host.docker.internal")
    .WithHelmValue("database.port", dbPort.Property(EndpointProperty.Port));
```

Inside the pod, `host.docker.internal` resolves to the Docker host because k3s runs as a
privileged container on the DCP network and Flannel masquerades outbound pod traffic
through it.

## Additional information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-k3s

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
