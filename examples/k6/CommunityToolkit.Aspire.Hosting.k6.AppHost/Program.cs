using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var apiservice = builder
    .AddProject<CommunityToolkit_Aspire_Hosting_k6_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

var k6 = builder.AddK6("k6")
    .WithBindMount("scripts", "/scripts", true)
    .WithScript("/scripts/main.js")
    .WithReference(apiservice)
    .WaitFor(apiservice);

builder.Build().Run();