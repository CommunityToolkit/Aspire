using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var goff = builder.AddGoFeatureFlag("goff")
    .WithGoffBindMount("./goff");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("apiservice", new CommunityToolkit_Aspire_Hosting_GoFeatureFlag_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(goff)
    .WaitFor(goff)
    .WithHttpHealthCheck("/health");

builder.Build().Run();