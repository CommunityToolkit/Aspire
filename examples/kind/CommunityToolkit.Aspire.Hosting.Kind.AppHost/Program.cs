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

// Test Aspire-container → Kind-workload connectivity by pinging Redis
// through the Kind container network on the NodePort.
builder.AddContainer("redis-ping", "nicolaka/netshoot")
    .WithKindNetwork()
    .WaitFor(cluster)
    .WithEntrypoint("sh")
    .WithArgs("-c", "while true; do nc -zv kind-cluster-control-plane 30379; sleep 5; done");

builder.Build().Run();
