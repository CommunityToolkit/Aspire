var builder = DistributedApplication.CreateBuilder(args);
var manifestMountSource = Path.Combine(builder.AppHostDirectory, "manifests");

// Kind cluster as a managed dependency (F5 mode).
// The cluster appears in the Aspire dashboard, your apps get KUBECONFIG injected.
var cluster = builder.AddKindCluster("kind-cluster")
    .WithNodeImage("kindest/node:v1.32.2")
    // Configuration example: mount the same manifest directory into each Kind node.
    // This sample still applies manifests from the host path below rather than from inside a workload.
    .WithNodeMount(manifestMountSource, "/var/local/aspire/manifests", readOnly: true);

// Run Headlamp (a lightweight Kubernetes web UI) as an Aspire-managed container
// connected to the Kind cluster.
var dashboard = builder.AddContainer("headlamp", "ghcr.io/headlamp-k8s/headlamp:latest")
    .WithHttpEndpoint(targetPort: 4466)
    .WithReference(cluster);

// Deploy a Helm chart to the Kind cluster, exposed via NodePort so containers
// on the Kind container network can reach it.
var redis = cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
    .WithHelmValue("replica.replicaCount", "0")
    .WithHelmValue("master.service.type", "NodePort")
    .WithHelmValue("master.service.nodePorts.redis", "30379")
    .WithHelmStringValue("auth.password", "000123")
    .WithCrdWaitRetry(maxAttempts: 3, backoff: TimeSpan.FromSeconds(5))
    .WithNamespace("cache");

// Apply raw Kubernetes YAML from the same host directory mounted into each Kind node.
var manifestResource = cluster.AddManifest("extra-config", Path.Combine(manifestMountSource, "extra-config.yaml"))
    .WithClusterReadyTimeout(TimeSpan.FromMinutes(2))
    .WithNamespace("aspire-demo");

// Demonstrate recursive directory apply for manifest folders.
cluster.AddManifest("recursive-config", manifestMountSource)
    .WithRecursive();

// Demonstrate server-side apply with conflict forcing and a stable field manager.
cluster.AddManifest("ssa-config", Path.Combine(manifestMountSource, "extra-config.yaml"))
    .WithServerSideApply(forceConflicts: true)
    .WithFieldManager("aspire-example");

// Demonstrate best-effort CRD waiting when a local demo should continue after a CRD wait timeout.
cluster.AddManifest("best-effort-crds", Path.Combine(manifestMountSource, "extra-config.yaml"))
    .WithCrdWaitBehavior(CrdWaitBehavior.BestEffort);

cluster.AddManifestFromContent("demo-ns", """
    apiVersion: v1
    kind: Namespace
    metadata:
      name: aspire-demo
    """);

// Test Aspire-container → Kind-workload connectivity by pinging Redis
// through the Kind container network on the NodePort.
builder.AddContainer("redis-ping", "nicolaka/netshoot")
    .WithKindNetwork()
    .WaitFor(cluster)
    // Wait for the manifest resource before starting a downstream container.
    .WaitFor(manifestResource)
    .WithEntrypoint("sh")
    .WithArgs("-c", "while true; do nc -zv kind-cluster-control-plane 30379; sleep 5; done");

builder.Build().Run();