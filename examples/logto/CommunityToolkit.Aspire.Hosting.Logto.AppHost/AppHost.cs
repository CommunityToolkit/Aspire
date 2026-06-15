var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");

var cache = builder.AddRedis("redis");

var logto = builder.AddLogto("logto", postgres)
    .WithRedis(cache)
    .WithDatabaseSeeding();


var clientOIDC = builder.AddProject<Projects.CommunityToolkit_Aspire_Logto_ClientOIDC>("clientOIDC")
    .WithReference(logto)
    .WaitFor(logto);
var clientJWT = builder.AddProject<Projects.CommunityToolkit_Aspire_Logto_ClientJWT>("clientJWT")
    .WithReference(logto)
    .WaitFor(logto);


builder.Build().Run();
