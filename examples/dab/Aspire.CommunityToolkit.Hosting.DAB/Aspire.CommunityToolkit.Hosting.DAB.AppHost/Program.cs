using Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer("sql", sqlPassword, port: 1234)
    .WithDataVolume("MyDataVolume")
    .WithHealthCheck();
var sqlDatabase = sqlServer.AddDatabase("Database");

var sqlShell = "./sql-server";
var sqlScript = "../Database";
sqlServer
    .WithBindMount(sqlShell, target: "/usr/config")
    .WithBindMount(sqlScript, target: "/docker-entrypoint-initdb.d")
    .WithEntrypoint("/usr/config/entrypoint.sh");

var dab = builder.AddDataAPIBuilder("dab")
    .WithReference(sqlDatabase)
    .WaitFor(sqlServer);

builder.Build().Run();
