using Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer("sql", sqlPassword)
    //.WithDataVolume("MyDataVolume") // Uncomment to persist the data into volume
    .WithHealthCheck();
var sqlDatabase = sqlServer.AddDatabase("trek");

sqlServer
    .WithBindMount("./sql-server", target: "/usr/config")
    .WithBindMount("../Database/sql", target: "/docker-entrypoint-initdb.d")
    .WithEntrypoint("/usr/config/entrypoint.sh");

var dab = builder.AddDataAPIBuilder("dab")
    .WithReference(sqlDatabase)
    .WaitFor(sqlDatabase);
    
builder.Build().Run();
