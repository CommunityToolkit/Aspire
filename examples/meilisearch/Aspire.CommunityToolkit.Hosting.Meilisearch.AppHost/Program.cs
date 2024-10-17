using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var meilisearch = builder.AddMeilisearch("meilisearch");

builder.AddProject<Aspire_CommunityToolkit_Hosting_Meilisearch_ApiService>("apiservice")
    .WithReference(meilisearch);

builder.Build().Run();
