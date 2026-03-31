var builder = DistributedApplication.CreateBuilder(args);

// Create a Kind cluster with Kubernetes version v1.32.2.
var cluster = builder.AddKindCluster("kind-cluster")
    .WithKubernetesVersion("v1.32.2");

// Deploy a Helm chart to the Kind cluster.
var redis = cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
    .WithHelmValue("replica.replicaCount", "0")
    .WithNamespace("cache");

builder.Build().Run();
