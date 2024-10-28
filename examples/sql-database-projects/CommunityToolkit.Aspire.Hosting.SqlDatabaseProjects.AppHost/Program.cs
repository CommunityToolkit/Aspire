var builder = DistributedApplication.CreateBuilder(args);

builder.AddSqlProject<Projects.SdkProject>("SdkProject");

builder.Build().Run();