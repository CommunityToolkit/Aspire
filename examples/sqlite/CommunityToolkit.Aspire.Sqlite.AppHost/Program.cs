var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite")
    .AddSqliteWeb()
    ;

var sqliteEF = builder.AddSqlite("sqlite-ef")
    .AddSqliteWeb()
    ;

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Sqlite_Api>("api")
    .WithReference(sqlite)
    .WithReference(sqliteEF);

builder.Build().Run();
