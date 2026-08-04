#pragma warning disable ASPIREPIPELINES001

var builder = DistributedApplication.CreateBuilder(args);

// Kind as a compute environment (aspire publish / aspire deploy).
// Generates Helm charts and deploys to a local Kind cluster.
//
//   aspire publish --output-path ./charts   # generate Helm chart
//   aspire deploy                           # deploy directly to Kind
builder.AddKubernetesEnvironment("kind-deploy")
    .WithKind()
    .WithKubernetesVersion("v1.32.2")
    .WithWorkerNodes(1);

builder.AddContainer("redis", "redis", "7");

builder.Build().Run();
