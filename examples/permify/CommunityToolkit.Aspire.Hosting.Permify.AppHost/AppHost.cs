var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("permify-postgres")
    .AddDatabase("permify-db");

builder.AddPermify("permify")
    .WithWatchSupport(database);

builder.Build().Run();