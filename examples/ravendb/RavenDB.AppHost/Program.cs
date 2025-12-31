using CommunityToolkit.Aspire.Hosting.RavenDB;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var serverSettings = RavenDBServerSettings.Unsecured();
var ravendb = builder.AddRavenDB("ravendb", serverSettings);
ravendb.AddDatabase("ravenDatabase", ensureCreated: true);

builder.AddProject<CommunityToolkit_Aspire_Hosting_RavenDB_ApiService>("apiservice")
    .WithReference(ravendb)
    .WaitFor(ravendb);

builder.Build().Run();
