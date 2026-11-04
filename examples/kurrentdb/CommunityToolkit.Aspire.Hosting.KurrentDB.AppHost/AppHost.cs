using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var kurrentdb = builder.AddKurrentDB("kurrentdb", 22113);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("apiservice", new CommunityToolkit_Aspire_Hosting_KurrentDB_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(kurrentdb)
    .WaitFor(kurrentdb);

builder.Build().Run();
