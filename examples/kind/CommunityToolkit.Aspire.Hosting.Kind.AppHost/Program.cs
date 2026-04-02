#pragma warning disable ASPIREPIPELINES001

var builder = DistributedApplication.CreateBuilder(args);

// Scenario 1: Kind cluster as a managed dependency (F5 mode)
// The cluster appears in the Aspire dashboard, your apps get KUBECONFIG injected.
var cluster = builder.AddKindCluster("kind-cluster")
    .WithKubernetesVersion("v1.32.2");

// Deploy a Helm chart to the Kind cluster during F5.
var redis = cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
    .WithHelmValue("replica.replicaCount", "0")
    .WithNamespace("cache");

// Scenario 2: Kind as a compute environment (aspire publish / aspire deploy)
// Generates Helm charts and deploys to a local Kind cluster.
builder.AddKubernetesEnvironment("kind-deploy")
    .WithKind()
    .WithKubernetesVersion("v1.32.2")
    .WithWorkerNodes(1);

builder.AddContainer("redis-container", "redis", "7");

builder.Build().Run();
