using CommunityToolkit.Aspire.Hosting.Logto;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var cache = builder.AddRedis("redis")
    .WithDataVolume();

var logto = builder.AddLogtoContainer("logto", postgres)
    .WithRedis(cache);


var clientOIDC = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Logto_ClientOIDC>("clientOIDC")
    .WithReference(logto)
    .WaitFor(logto);
var clientJWT = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Logto_ClientJWT>("clientJWT")
    .WithReference(logto)
    .WaitFor(logto);


builder.Build().Run();