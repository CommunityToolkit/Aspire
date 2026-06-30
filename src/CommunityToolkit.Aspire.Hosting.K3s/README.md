# CommunityToolkit.Aspire.Hosting.K3s

Provides extension methods and resource definitions for Aspire Distribute Apps to run a
[k3s](https://k3s.io/) lightweight Kubernetes cluster as part of the local development
inner loop. The cluster, Helm chart installs, manifest applies, and service endpoint
exposures all appear as first-class resources in the Aspire dashboard — no external
tooling beyond a supported container runtime is required.

## Prerequisites

A container runtime that supports privileged Linux containers:

- **Docker Engine 20.10+** (Linux) or **Docker Desktop** (macOS / Windows)
- **Podman 4.0+** (Linux, rootful only — rootless requires cgroup v2 delegation)

> k3s requires `--privileged`, `--cgroupns=host`, and a writable cgroup filesystem. These
> flags are passed automatically by the integration.

## Install

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.K3s
```

## Quick start

```csharp
var cluster = builder.AddK3sCluster("k8s");

builder.AddProject<Projects.MyOperator>("operator")
    .WaitFor(cluster)
    .WithReference(cluster);   // injects KUBECONFIG automatically
```

## Cluster configuration

All options are available as fluent builder methods — no callback required.

```csharp
var cluster = builder
    .AddK3sCluster("k8s",
        apiServerPort: 16443,   // fixed host port for the API server (random by default)
        agentCount: 2)          // 1 server + 2 agent nodes
    .WithK3sVersion("v1.32.3-k3s1")        // pin k3s image tag
    .WithPodSubnet("10.42.0.0/16")         // --cluster-cidr
    .WithServiceSubnet("10.43.0.0/16")     // --service-cidr
    .WithDisabledComponent("traefik")      // --disable=traefik (repeatable)
    .WithExtraArg("--write-kubeconfig-mode=644")   // raw k3s server flag (repeatable)
    .WithHelmImage(tag: "3.18.0")          // override the alpine/helm version
    .WithKubectlImage(tag: "1.37.0")       // override the alpine/kubectl version
    .WithDataVolume()                      // persist cluster state across restarts
    .WithLifetime(ContainerLifetime.Persistent);
```

### Option reference

| Method | Parameter / Effect |
|---|---|
| `AddK3sCluster(agentCount:)` | Number of worker nodes (0 = single-node). Equivalent to `WithAgentCount`. |
| `WithAgentCount(n)` | Same as above, fluent alternative. The health check waits for all `1 + n` nodes to be `Ready`. |
| `WithK3sVersion(tag)` | Overrides the k3s image tag, e.g. `v1.32.3-k3s1`. Synced to agents automatically. |
| `WithPodSubnet(cidr)` | Sets `--cluster-cidr`. Default: k3s built-in `10.42.0.0/16`. |
| `WithServiceSubnet(cidr)` | Sets `--service-cidr`. Default: k3s built-in `10.43.0.0/16`. |
| `WithDisabledComponent(c)` | Passes `--disable=<c>`. Call multiple times for multiple components. |
| `WithExtraArg(arg)` | Appends a raw argument to `k3s server`. |
| `WithHelmImage(registry?, image?, tag?)` | Overrides the `alpine/helm` image used by `AddHelmRelease`. |
| `WithKubectlImage(registry?, image?, tag?)` | Overrides the `alpine/kubectl` image used by `AddK8sManifest`. |
| `WithDataVolume(name?)` | Mounts a named Docker volume at `/var/lib/rancher/k3s`. |
| `WithLifetime(lifetime)` | Sets `ContainerLifetime.Persistent` or `Session` for cluster and agents. |

## Persistent cluster

```csharp
var cluster = builder.AddK3sCluster("k8s")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
```

`WithDataVolume` persists the k3s database, certificates, and node tokens across AppHost
restarts. `WithLifetime(Persistent)` tells DCP to keep the Docker container alive between
runs, making subsequent starts much faster.

> **Agent count and persistence**: with persistent containers, `agentCount` must stay
> constant across runs. Decreasing it leaves orphaned agent containers that will fail to
> rejoin the cluster; delete them manually with `docker rm -f`.

## Deploying Helm charts

`AddHelmRelease` runs `helm upgrade --install --wait` inside an `alpine/helm` container.
No host-side `helm` binary is required.

```csharp
var podinfo = cluster.AddHelmRelease(
    name: "podinfo",
    chart: "podinfo",
    repo: "https://stefanprodan.github.io/podinfo",
    version: "6.7.1",
    @namespace: "podinfo")
    .WithHelmValue("replicaCount", "2")
    .WithHelmValuesFile("./deploy/podinfo-values.yaml");

// Wait for the chart install to complete before starting the operator.
builder.AddProject<Projects.MyOperator>("operator")
    .WaitForCompletion(podinfo)
    .WithReference(cluster);
```

### Helm value precedence

Values are applied in this order (last wins):

1. `WithHelmValuesFile` — in declaration order
2. `WithHelmValue` (`--set` flags) — always override files

Use `WithHelmValuesFile` for structured overrides (values with commas, braces, or
backslashes). `WithHelmValue` is convenient for individual scalar overrides.

## Applying Kubernetes manifests

`AddK8sManifest` runs `kubectl apply --server-side` inside an `alpine/kubectl` container.
The apply mode is detected automatically from the path:

| Path | Mode |
|---|---|
| Single `.yaml` / `.yml` file | `kubectl apply -f <file>` |
| Directory (no `kustomization.yaml`) | `kubectl apply -f <dir>` (all YAML files, lexicographic order) |
| Directory containing `kustomization.yaml` | `kubectl apply -k <dir>` (Kustomize) |

```csharp
// Plain YAML
var appConfig = cluster.AddK8sManifest("app-config", "./k8s/app-config.yaml")
    .WaitForCompletion(podinfo);

// Kustomize overlay — auto-detected; directory bind-mounted to preserve base references
var monitoring = cluster.AddK8sManifest("monitoring-config", "./k8s/monitoring")
    .WaitForCompletion(podinfo)
    .WaitForCompletion(appConfig);

// Wait for CRD to be Established before starting the operator
builder.AddProject<Projects.WidgetOperator>("operator")
    .WaitForCompletion(crd)
    .WithReference(cluster);
```

## Exposing k8s services

`AddServiceEndpoint` starts an in-process WebSocket port-forward bound to
`0.0.0.0:{allocatedPort}`. No NodePort or LoadBalancer configuration is required.
The endpoint transitions to `Running` only after the target service has a ready pod.

```csharp
var podinfoWeb = cluster
    .AddServiceEndpoint("podinfo-web", "podinfo", servicePort: 9898, @namespace: "podinfo")
    .WaitForCompletion(podinfo);

// Host processes receive http://localhost:{port}
builder.AddProject<Projects.MyApi>("api")
    .WaitFor(podinfoWeb)
    .WithReference(podinfoWeb);

// DCP-network containers receive http://host.docker.internal:{port}
// --add-host=host.docker.internal:host-gateway is injected automatically
builder.AddContainer("sidecar", "myorg/sidecar")
    .WaitFor(podinfoWeb)
    .WithReference(podinfoWeb);
```

The injected environment variable follows the Aspire service-discovery convention:
`services__{name}__url=http(s)://{host}:{port}`.

Scheme is inferred from the port: 443 and 8443 → `https`, all others → `http`. Override
with the `scheme` parameter: `AddServiceEndpoint("ep", "svc", 8080, scheme: "https")`.

## Kubeconfig injection

Both `K3sClusterResource` and `K3sServiceEndpointResource` implement
`IResourceWithConnectionString`, so the standard Aspire `WithReference` overload handles
credential injection automatically.

```csharp
// Projects and executables receive KUBECONFIG pointing to the host-accessible variant
builder.AddProject<Projects.MyOperator>("operator")
    .WithReference(cluster);   // KUBECONFIG=…/.k3s/k8s/local/kubeconfig.yaml

// Containers receive a bind-mounted kubeconfig at /tmp/k3s-kubeconfig.yaml
builder.AddContainer("sidecar", "myorg/sidecar")
    .WithReference(cluster);   // KUBECONFIG=/tmp/k3s-kubeconfig.yaml + file bind-mount
```

All standard Kubernetes tooling reads `KUBECONFIG` automatically:

```csharp
var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
    Environment.GetEnvironmentVariable("KUBECONFIG"));
using var client = new Kubernetes(config);
```

## TypeScript polyglot AppHost

All cluster options are available as fluent methods in the TypeScript SDK — no callback
or `K3sClusterOptions` type required.

```typescript
import { createBuilder, ContainerLifetime } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();

const cluster = await builder
    .addK3sCluster('k8s', { agentCount: 2 })
    .withK3sVersion('v1.32.3-k3s1')
    .withPodSubnet('10.42.0.0/16')
    .withDisabledComponent('traefik')
    .withHelmImage({ tag: '3.18.0' })
    .withDataVolume({ name: 'k8s-data' })
    .withLifetime(ContainerLifetime.Persistent);

const podinfo = await cluster.addHelmRelease('podinfo', 'podinfo', {
    repo: 'https://stefanprodan.github.io/podinfo',
    version: '6.7.1',
    namespace: 'podinfo',
});

const podinfoWeb = await cluster
    .addServiceEndpoint('podinfo-web', 'podinfo', 9898, { namespace: 'podinfo' })
    .waitForCompletion(podinfo);

// Standard withReference — injects KUBECONFIG or services__name__url
await builder.addProject('operator', '../MyOperator/MyOperator.csproj')
    .withReference(cluster);

await builder.addProject('api', '../MyApi/MyApi.csproj')
    .withReference(podinfoWeb);

await builder.build().run();
```

## Reaching Aspire services from k3s pods

k3s pods run on the internal pod network (`10.42.0.0/16`). Flannel masquerades outbound
pod traffic through the k3s container's DCP network IP, so pods can reach DCP services
using `host.docker.internal` and the host-mapped port.

```csharp
var postgres = builder.AddPostgres("db");

cluster.AddHelmRelease("my-operator", "my-operator-chart")
    .WithHelmValue("database.host", "host.docker.internal")
    .WithHelmValue("database.port",
        postgres.GetEndpoint("tcp").Property(EndpointProperty.Port));
```

## Additional information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-k3s

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
