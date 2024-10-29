var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql")
                    .AddDatabase("TargetDatabase");

builder.AddSqlProject<Projects.SdkProject>("sdk-project")
       .PublishTo(server);

builder.Build().Run();
