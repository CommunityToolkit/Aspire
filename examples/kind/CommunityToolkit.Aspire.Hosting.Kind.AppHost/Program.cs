var builder = DistributedApplication.CreateBuilder(args);

// Kind cluster as a managed dependency (F5 mode).
// The cluster appears in the Aspire dashboard, your apps get KUBECONFIG injected.
var cluster = builder.AddKindCluster("kind-cluster")
    .WithKubernetesVersion("v1.32.2");

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
    .WithNamespace("cache");

// Apply raw Kubernetes YAML to the Kind cluster and target the namespace declared by the sample manifest.
var manifestResource = cluster.AddManifest("extra-config", "manifests/extra-config.yaml")
    .WithNamespace("aspire-demo");

// Demonstrate recursive directory apply for manifest folders.
cluster.AddManifest("recursive-config", "manifests")
    .WithRecursive();

// Demonstrate server-side apply with conflict forcing and a stable field manager.
cluster.AddManifest("ssa-config", "manifests/extra-config.yaml")
    .WithServerSideApply(forceConflicts: true)
    .WithFieldManager("aspire-example");

// Demonstrate best-effort CRD waiting when a local demo should continue after a CRD wait timeout.
cluster.AddManifest("best-effort-crds", "manifests/extra-config.yaml")
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
