using CommunityToolkit.Aspire.Hosting.Logto;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var cache = builder.AddRedis("redis")
    .WithDataVolume();

var logto = builder.AddLogtoContainer("logto", postgres)
    .WithRedis(cache);

builder.Build().Run();