using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var eventstore = builder.AddEventStore("eventstore", 22113);

builder.AddProject<CommunityToolkit_Aspire_Hosting_EventStore_ApiService>("apiservice")
    .WithReference(eventstore)
    .WaitFor(eventstore);

builder.Build().Run();
