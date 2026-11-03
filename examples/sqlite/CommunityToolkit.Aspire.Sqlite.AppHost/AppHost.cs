var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite")
    .WithSqliteWeb()
    ;

var sqliteEF = builder.AddSqlite("sqlite-ef")
    .WithSqliteWeb()
    ;

#pragma warning disable ASPIREDOTNETPROJECT001
var api = builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_Sqlite_Api().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(sqlite)
    .WithReference(sqliteEF);

builder.Build().Run();
