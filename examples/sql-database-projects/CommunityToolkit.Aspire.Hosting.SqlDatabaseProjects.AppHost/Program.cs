var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql");

var database = server.AddDatabase("TargetDatabase");

var otherDatabase = server.AddDatabase("OtherTargetDatabase");

var sdkProject = builder.AddSqlProject<Projects.SdkProject>("sdk-project")
       .WithReference(database);

var otherProject = builder.AddSqlProject<Projects.SdkProject>("other-sdk-project")
       .WithReference(otherDatabase)
       .WaitForCompletion(sdkProject);

builder.AddSqlPackage<Packages.ErikEJ_Dacpac_Chinook>("chinook")
       .WithReference(database);

builder.Build().Run();
