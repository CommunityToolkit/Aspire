using CommunityToolkit.Aspire.Hosting.k6;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREDOTNETPROJECT001
var apiservice = builder
    .AddDotnetProject("apiservice", new CommunityToolkit_Aspire_Hosting_k6_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithHttpHealthCheck("/health");

var k6 = builder.AddK6("k6")
    .WithBindMount("scripts", "/scripts", true)
    .WithScript("/scripts/main.js", summaryMode: K6SummaryMode.Full)
    .WithReference(apiservice)
    .WaitFor(apiservice);

builder.Build().Run();