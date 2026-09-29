var builder = DistributedApplication.CreateBuilder(args);

var duckdb = builder.AddDuckDB("analytics");

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_DuckDB_Api>("api")
    .WithReference(duckdb);

builder.Build().Run();
