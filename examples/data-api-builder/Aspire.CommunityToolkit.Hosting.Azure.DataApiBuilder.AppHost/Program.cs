var builder = DistributedApplication.CreateBuilder(args);

// Add a SQL Server container
var sqlServer = builder
    .AddSqlServer("sql")
    .WithHealthCheck();

var sqlDatabase = sqlServer.AddDatabase("trek");

// Populate the database with the schema and data
sqlServer
    .WithBindMount("./sql-server", target: "/usr/config")
    .WithBindMount("../database", target: "/docker-entrypoint-initdb.d")
    .WithEntrypoint("/usr/config/entrypoint.sh");

// Add Data API Builder using dab-config.json 
var dab = builder.AddDataAPIBuilder("dab")
    .WaitFor(sqlServer)
    .WithReference(sqlDatabase);

builder.AddProject<Projects.Aspire_CommunityToolkit_Hosting_Azure_DataApiBuilder_BlazorApp>("blazorApp")
    .WithReference(dab);

builder.Build().Run();
