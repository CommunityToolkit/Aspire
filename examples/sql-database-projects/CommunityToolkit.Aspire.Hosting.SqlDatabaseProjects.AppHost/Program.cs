var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql")
                    .AddDatabase("TargetDatabase");

builder.AddSqlProject<Projects.SdkProject>("SdkProject")
       .PublishTo(server);

builder.Build().Run();
