using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var chroma = builder.AddChroma("chroma");

builder.AddProject<CommunityToolkit_Aspire_Hosting_Chroma_ApiService>("apiservice")
    .WithReference(chroma)
    .WaitFor(chroma)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
