var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite")
    // .AddSqliteWeb()
    ;

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Sqlite_Api>("api")
    .WithReference(sqlite);

builder.Build().Run();
