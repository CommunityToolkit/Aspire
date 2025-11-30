var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres");

builder.AddZitadel("zitadel")
    .WithDatabase(database);

builder.Build().Run();