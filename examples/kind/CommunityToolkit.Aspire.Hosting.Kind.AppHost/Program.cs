var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddKindCluster("kind-cluster")
    .WithKubernetesVersion("v1.32.2");

// Deploy a Helm chart to the Kind cluster.
// Uncomment and adjust the chart reference for your use case:
//
// var redis = cluster.AddHelmChart("redis", "oci://registry-1.docker.io/bitnamicharts/redis")
//     .WithChartVersion("20.0.0")
//     .WithHelmValue("replica.replicaCount", "0")
//     .WithNamespace("cache");

builder.Build().Run();
