using Aspire.Hosting;

// CommunityToolkit.Aspire.Hosting.Squad — example AppHost
//
// Shows the minimal usage of AddSquad to surface a Squad AI-agent team as a
// first-class Aspire resource. The sample uses the bundled `sample-squad/`
// workspace so it runs without any external state.

var builder = DistributedApplication.CreateBuilder(args);

var repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));
var sampleSquadRoot = Path.Combine(builder.AppHostDirectory, "sample-squad");

builder.AddSquad("research-squad", teamRoot: sampleSquadRoot);

builder.Build().Run();
