var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite").AddSqliteWeb();

builder.Build().Run();
