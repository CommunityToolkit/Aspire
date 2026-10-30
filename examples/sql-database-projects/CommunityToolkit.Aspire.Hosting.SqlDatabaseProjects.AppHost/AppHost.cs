var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql");

var database = server.AddDatabase("Database1");

var chinookDatabase = server.AddDatabase("Database2");

var otherDatabase = server.AddDatabase("Database3");

var sdkProject = builder.AddSqlProject("sdk-project", "../SdkProject/SdkProject.csproj")
       .WithReference(database);

var otherProject = builder.AddSqlProject("other-sdk-project", "../SdkProject/SdkProject.csproj")
       .WithReference(otherDatabase)
       .WaitForCompletion(sdkProject);

builder.AddSqlPackage("chinook", "ErikEJ.Dacpac.Chinook", "1.0.0")
       .WithReference(chinookDatabase);

var connection = builder.AddConnectionString("Aspire");

builder.AddSqlProject("existing-db", "../SdkProject/SdkProject.csproj")
       .WithReference(connection)
       .WithExplicitStart();

var delayedProject = builder.AddSqlProject("sdk-project-delayed", "../SdkProject/SdkProject.csproj")
       .WithReference(database)
       .WithExplicitStart();

builder.Build().Run();
