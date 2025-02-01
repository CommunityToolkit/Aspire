using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var goff = builder.AddGoFeatureFlag("goff")
    .WithGoffBindMount("./goff");

builder.AddProject<CommunityToolkit_Aspire_Hosting_GoFeatureFlag_ApiService>("apiservice")
    .WithReference(goff)
    .WaitFor(goff)
    .WithHttpHealthCheck("/health");

builder.Build().Run();