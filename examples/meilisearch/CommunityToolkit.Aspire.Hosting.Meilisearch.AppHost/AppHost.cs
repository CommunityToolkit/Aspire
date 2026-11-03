using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var meilisearch = builder.AddMeilisearch("meilisearch");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("apiservice", new CommunityToolkit_Aspire_Hosting_Meilisearch_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(meilisearch)
    .WaitFor(meilisearch)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
