var builder = DistributedApplication.CreateBuilder(args);

// Add a SQL Server container
var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer("sql", sqlPassword)
    //.WithDataVolume("MyDataVolume") // Uncomment to persist the data into volume
    .WithHealthCheck();
var sqlDatabase = sqlServer.AddDatabase("trek");

// Populate the database with the schema and data
sqlServer
    .WithBindMount("./sql-server", target: "/usr/config")
    .WithBindMount("../database", target: "/docker-entrypoint-initdb.d")
    .WithEntrypoint("/usr/config/entrypoint.sh");

// Add Data API Builder using dab-config.json 
var dab = builder.AddDataAPIBuilder("dab")
    .WithReference(sqlDatabase)
    .WaitFor(sqlServer);

builder.AddProject<Projects.Aspire_CommunityToolkit_Hosting_DAB_BlazorApp>("blazorApp")
        .WithReference(dab);
    
builder.Build().Run();
