using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var kurrentdb = builder.AddKurrentDB("kurrentdb", 22113);

builder.AddProject<CommunityToolkit_Aspire_Hosting_KurrentDB_ApiService>("apiservice")
    .WithReference(kurrentdb)
    .WaitFor(kurrentdb);

builder.Build().Run();
