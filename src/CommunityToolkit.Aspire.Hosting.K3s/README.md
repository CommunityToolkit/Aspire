# CommunityToolkit.Aspire.Hosting.K3s

An Aspire hosting integration for [k3s](https://k3s.io/) — a lightweight, certified Kubernetes distribution by Rancher/SUSE.

## Getting started

### Prerequisites

- Docker with support for `--privileged` containers (Linux host or Docker Desktop on macOS/Windows)

### Installation

```sh
dotnet add package CommunityToolkit.Aspire.Hosting.K3s
```

## Usage

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddK3sCluster("k8s")
    .WithPersistentState();

builder.AddProject<Projects.MyApi>("api")
    .WithReference(cluster)
    .WaitFor(cluster);

builder.Build().Run();
```

### Kubeconfig injection

`WithReference(cluster)` automatically selects the injection mode:

| Resource type | Environment variable set |
|---|---|
| `ProjectResource` / `ExecutableResource` | `KUBECONFIG=/tmp/aspire-k3s-k8s/admin.yaml` |
| `ContainerResource` | `KUBECONFIG_DATA=<base64>` |

### Configuration options

```csharp
builder.AddK3sCluster("k8s", configure: opts =>
{
    opts.ClusterCidr = "10.42.0.0/16";
    opts.ServiceCidr = "10.43.0.0/16";
    opts.DisabledComponents.Add("traefik");
});
```

Or use the fluent API:

```csharp
builder.AddK3sCluster("k8s")
    .WithK3sVersion("v1.32.3-k3s1")
    .WithPodSubnet("10.42.0.0/16")
    .WithServiceSubnet("10.43.0.0/16")
    .WithDisabledComponent("traefik")
    .WithPersistentState();
```

## Known limitations

- Requires a privileged Docker runtime; `--privileged` is passed automatically.
- On Linux hosts the `/lib/modules` directory should be present for CNI networking.
- The first cluster start can take 30–60 seconds while container images and CNI plugins are initialised.
