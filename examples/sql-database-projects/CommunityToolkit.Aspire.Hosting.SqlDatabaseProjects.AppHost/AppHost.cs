var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql");

var database = server.AddDatabase("Database1");

var chinookDatabase = server.AddDatabase("Database2");

var packageDatabase = server.AddDatabase("PackageDatabase");

var otherDatabase = server.AddDatabase("Database3");

var sdkProject = builder.AddSqlProject<Projects.SdkProject>("sdk-project")
       .WithReference(database);

var otherProject = builder.AddSqlProject<Projects.SdkProject>("other-sdk-project")
       .WithReference(otherDatabase)
       .WaitForCompletion(sdkProject);

builder.AddSqlPackage<Packages.ErikEJ_Dacpac_Chinook>("chinook")
       .WithReference(chinookDatabase);

builder.AddSqlPackage("chinook-package", "ErikEJ.Dacpac.Chinook", "1.0.0")
       .WithReference(packageDatabase);

var connection = builder.AddConnectionString("Aspire");

builder.AddSqlProject<Projects.SdkProject>("existing-db")
        .WithReference(connection);

var delayedProject = builder.AddSqlProject<Projects.SdkProject>("sdk-project-delayed")
       .WithReference(database)
       .WithExplicitStart();

builder.AddSqlProject("microsoft-build-sql-project", "../MicrosoftBuildSqlProject/MicrosoftBuildSqlProject.sqlproj")
       .WithReference(database);

builder.Build().Run();
