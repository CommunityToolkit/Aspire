using CommunityToolkit.Aspire.Hosting.RavenDB;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var serverSettings = RavenDBServerSettings.Unsecured();
var ravendb = builder.AddRavenDB("ravendb", serverSettings);
ravendb.AddDatabase("ravenDatabase");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("apiservice", new CommunityToolkit_Aspire_Hosting_RavenDB_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(ravendb)
    .WaitFor(ravendb);

builder.Build().Run();
