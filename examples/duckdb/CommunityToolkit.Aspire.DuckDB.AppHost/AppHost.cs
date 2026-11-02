var builder = DistributedApplication.CreateBuilder(args);

var duckdb = builder.AddDuckDB("analytics");

#pragma warning disable ASPIREDOTNETPROJECT001
var api = builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_DuckDB_Api().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(duckdb);

builder.Build().Run();
