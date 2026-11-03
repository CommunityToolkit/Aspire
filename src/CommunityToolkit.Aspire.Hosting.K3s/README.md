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
| `WithEnabledComponent(c)` | Takes back a disable. `servicelb` and `metrics-server` are disabled by default. |
| `WithIngressDeveloperCertificate()` | Traefik serves the AppHost's developer certificate; release namespaces get it as a Secret. |
| `WithHostAlias(resource, hostName?)` | Pods resolve an Aspire container by name. |
| `WithCoreDnsCustomization(file, content)` | Adds a file to the `coredns-custom` ConfigMap. |
| `WithExtraArg(arg)` | Appends a raw argument to `k3s server`. |
| `WithHelmImage(registry?, image?, tag?)` | Overrides the `alpine/helm` image used by `AddHelmRelease`. |
| `WithKubectlImage(registry?, image?, tag?)` | Overrides the `alpine/kubectl` image used by `AddK8sManifest`. |
| `WithDataVolume(name?)` | Mounts named volumes at `/var/lib/rancher/k3s` and `/etc/rancher/node`, and fixes the node name. |
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

`WithDataVolume` also keeps the server node's identity: it registers under the resource name
(`--node-name`) and its node password lives in a second volume. A recreated container — a new k3s
version, a changed argument — then rejoins as the same node, and persistent volumes bound to it stay
usable.

> **Data volumes created before the node name was fixed**: the node in such a volume is named after
> its old container's hostname, and its password lived only in that container, so neither can be
> recovered once the container is recreated. The first start after upgrading registers a new node
> under the resource name, and pods whose persistent volumes are bound to the old node stay
> `Pending`. Delete the old node (`kubectl delete node <old-name>`) and recreate those
> PersistentVolumeClaims, or remove the data volume to start fresh. Later recreations keep the node.

## Built-in components

`servicelb` and `metrics-server` are disabled by default, because they are the slowest to start.
Turn them back on when a workload needs them:

```csharp
var cluster = builder.AddK3sCluster("k8s")
    .WithEnabledComponent("servicelb")       // LoadBalancer Services bind the node's ports
    .WithEnabledComponent("metrics-server")  // HorizontalPodAutoscaler, kubectl top
    // The bundled Traefik, published from the cluster container on the host's 80 and 443.
    .WithEndpoint(port: 80, targetPort: 80, name: "web", scheme: "tcp", isProxied: false)
    .WithEndpoint(port: 443, targetPort: 443, name: "websecure", scheme: "tcp", isProxied: false);
```

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

### Charts from a directory

Pass the path of a chart directory — one containing `Chart.yaml`, relative to the AppHost project
directory — to install the chart you are working on:

```csharp
cluster.AddHelmRelease("my-app", "../../charts/my-app", @namespace: "my-app");
```

The directory is mounted read-only and copied inside the Helm container, where its dependencies are
fetched with `helm dependency build` — every HTTP repository its dependencies come from is added
first — so nothing has to be built on the host. A dependency on another chart directory, such as
`repository: file://../common`, is mounted beside it at the same relative path, and its own
dependencies are built first. A chart directory has its own version, so `version` is refused.

### Values resolved at run time

`WithHelmValue` also takes a parameter, an expression or an endpoint. Each reaches the Helm container
as an environment variable and is escaped for `--set` there, so a secret never lands in the generated
install script:

```csharp
var password = builder.AddParameter("db-password", secret: true);

cluster.AddHelmRelease("my-app", "oci://registry.example.com/charts/my-app")
    .WithHelmValue("database.password", password)
    .WithHelmValue("database.port", ReferenceExpression.Create($"{postgres.GetEndpoint("tcp").Property(EndpointProperty.TargetPort)}"));
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

## Serving the developer certificate

`WithIngressDeveloperCertificate` makes the AppHost's developer certificate — the one the machine trusts —
the default certificate of the bundled Traefik ingress controller. It carries `*.dev.localhost`, so hosts
published under that domain open in a browser without a warning:

```csharp
var cluster = builder.AddK3sCluster("k8s")
    .WithEnabledComponent("servicelb")
    .WithEndpoint(port: 443, targetPort: 443, name: "websecure", scheme: "tcp", isProxied: false)
    .WithIngressDeveloperCertificate();

cluster.AddHelmRelease("my-app", "../../charts/my-app", @namespace: "my-app")
    .WithHelmValue("ingress.host", "my-app.dev.localhost")
    .WithHelmValue("ingress.tls.secretName", "aspire-developer-certificate");
```

The certificate is stored as the TLS Secret `aspire-developer-certificate` in `kube-system` and in the
namespace of every Helm release of the cluster, so a chart can name it for its ingress and mount its
`tls.crt` to trust it — workloads that call each other through the ingress need that, because the
certificate is self-signed. The Secret holds the private key: this is for local development only, and
applies in run mode. Every Helm release and manifest waits for it; it is stored again on every start.

## Reaching Aspire services from k3s pods

Pods resolve names through the cluster's CoreDNS, which forwards to the node's resolver — inside a
container, Docker's embedded DNS on a loopback address that pods cannot reach. So a pod resolves
neither another Aspire container's name nor `host.docker.internal`. `WithHostAlias` makes a
container resolvable from every pod, under its resource name or a name you choose:

```csharp
var postgres = builder.AddPostgres("postgres");

var cluster = builder.AddK3sCluster("k8s")
    .WithHostAlias(postgres);                           // pods resolve "postgres"

cluster.AddHelmRelease("my-app", "oci://registry.example.com/charts/my-app")
    .WithHelmValue("database.host", "postgres")
    .WithHelmValue("database.port", "5432");            // the container port, not a host port
```

The container's address is looked up on the Aspire network each time the AppHost starts and written
into the `coredns-custom` ConfigMap, which k3s's CoreDNS imports; CoreDNS is then restarted. Every Helm
release and manifest of the cluster waits for that, whichever order they are declared in.

`WithCoreDnsCustomization` adds your own file to the same ConfigMap — a `.override` file for plugins in
the main server block, a `.server` file for a zone of its own:

```csharp
cluster.WithCoreDnsCustomization("my-app.override",
    "rewrite name regex (.*\\.)?my-app\\.localhost\\.? traefik.kube-system.svc.cluster.local. answer auto");
```

The cluster owns `coredns-custom` once either method is used, so a chart that also creates it conflicts;
move that configuration here.

## Additional information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-k3s

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
