using Aspire.Hosting;

// CommunityToolkit.Aspire.Hosting.Squad — example AppHost
//
// Demonstrates two real Squad teams as Aspire resources, both consumed by the
// same ApiApp via WithReference. The downstream API exposes /ask and /dispatch
// endpoints that take a ?squad= query parameter to pick which team handles the
// request, so you can see two distinct multi-agent personalities side-by-side
// in the same dashboard trace view.
//
//  - research-squad — AI/ML research squad cast from The Matrix
//      (Morpheus / Trinity / Oracle / Tank + Scribe / Ralph / Rai)
//
//  - dev-squad      — full-stack software development squad cast from The Simpsons
//      (Lisa / Marge / Frink / Comic Book Guy + Scribe / Ralph / Rai)
//
// Each folder is a fully-initialised Squad team (.squad/team.md, per-agent
// charters under .squad/agents/, casting registry under .squad/casting/), cast
// non-interactively via `squad init --no-workflows` + `copilot --yolo`. They live
// inside the repo so the example is self-contained and reproducible.

var builder = DistributedApplication.CreateBuilder(args);

// Both squads are folders next to this AppHost project so they ship with the
// example. AppHostDirectory resolves to .../CommunityToolkit.Aspire.Hosting.Squad.AppHost/.
var researchSquadRoot = Path.Combine(builder.AppHostDirectory, "research-squad");
var devSquadRoot      = Path.Combine(builder.AppHostDirectory, "dev-squad");

// 1) Two logical Squad resources, each surfaces in the dashboard as its own row
//    with the per-agent roster discovered from .squad/team.md.
var researchSquad = builder.AddSquad("research-squad", teamRoot: researchSquadRoot);
var devSquad      = builder.AddSquad("dev-squad",      teamRoot: devSquadRoot);

// 2) Downstream project that uses Squad.Agents.AI to drive whichever squad the
//    caller picks. Both squads are referenced so both connection strings are
//    injected into the ApiApp; the ApiApp's /ask and /dispatch endpoints take a
//    ?squad=research|dev query parameter to choose at request time.
builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Squad_ApiApp>("squad-api")
    .WithReference(researchSquad)
    .WithReference(devSquad);

builder.Build().Run();
