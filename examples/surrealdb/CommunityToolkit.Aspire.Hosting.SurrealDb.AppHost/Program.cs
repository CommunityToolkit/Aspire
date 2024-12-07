using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSurrealServer("surreal")
    .WithSurrealist()
    .AddNamespace("ns")
    .AddDatabase("db");

builder.AddProject<CommunityToolkit_Aspire_Hosting_SurrealDb_ApiService>("apiservice")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
