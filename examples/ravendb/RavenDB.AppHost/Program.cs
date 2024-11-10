using CommunityToolkit.Aspire.Hosting.RavenDB;

var builder = DistributedApplication.CreateBuilder(args);

var serverSettings = RavenDBServerSettings.Unsecured();
builder.AddRavenDB("ravenServer", serverSettings).AddDatabase("TestDatabase");

builder.Build().Run();
