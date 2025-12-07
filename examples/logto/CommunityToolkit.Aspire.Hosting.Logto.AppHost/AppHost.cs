using CommunityToolkit.Aspire.Hosting.Logto;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var cache = builder.AddRedis("redis")
    .WithDataVolume();

var logto = builder.AddLogtoContainer("logto", postgres)
    .WithRedis(cache);


var client = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Logto_ClientOIDC>("clientapi")
    .WithReference(logto)
    .WaitFor(logto);

builder.Build().Run();