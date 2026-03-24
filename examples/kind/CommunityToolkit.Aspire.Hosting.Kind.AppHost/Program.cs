var builder = DistributedApplication.CreateBuilder(args);

builder.AddKindCluster("kind-cluster")
    .WithKubernetesVersion("v1.32.2");

builder.Build().Run();
