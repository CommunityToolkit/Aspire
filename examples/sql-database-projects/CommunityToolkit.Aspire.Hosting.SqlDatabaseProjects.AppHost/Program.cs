var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql")
                    .AddDatabase("TargetDatabase");

builder.AddSqlProject<Projects.SdkProject>("sdk-project")
       .WithReference(server);

builder.AddSqlPackage<Packages.ErikEJ_Dacpac_Chinook>("chinook")
       .WithReference(server);

builder.Build().Run();
