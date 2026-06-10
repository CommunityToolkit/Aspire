using Aspire.Hosting;

// CommunityToolkit.Aspire.Hosting.Squad — example AppHost
//
// Shows two things in one dashboard run:
//
//  1. AddSquad surfaces a Squad AI-agent team as a first-class Aspire resource
//     (row in the dashboard, agent roster as resource properties).
//
//  2. A downstream ApiApp project consumes that resource via WithReference,
//     receives the squad://... connection string, and uses Squad.Agents.AI
//     to drive a real 3-turn conversation against the referenced team.
//
// Open the dashboard, find the ApiApp row, hit POST /ask, and you should see
// the agent's responses — including session memory across turns.

var builder = DistributedApplication.CreateBuilder(args);

var sampleSquadRoot = Path.Combine(builder.AppHostDirectory, "sample-squad");

// 1) Logical Squad resource.
var researchSquad = builder.AddSquad("research-squad", teamRoot: sampleSquadRoot);

// 2) Downstream project that uses the Squad via Squad.Agents.AI.
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Squad_ApiApp>("squad-api")
    .WithReference(researchSquad);

builder.Build().Run();
