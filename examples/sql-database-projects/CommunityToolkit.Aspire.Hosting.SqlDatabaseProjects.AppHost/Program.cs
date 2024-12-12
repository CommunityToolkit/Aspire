var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql")
                    .AddDatabase("TargetDatabase");

builder.AddSqlProject<Projects.SdkProject>("sdk-project")
       .WithReference(server);

builder.AddSqlPackage<Packages.Microsoft_SqlServer_Dacpacs>("dacpac-project", "tools/master.dacpac")
       .WithReference(server);

builder.Build().Run();
