# CommunityToolkit.Aspire.Hosting.Squad

An Aspire hosting integration that lets you model a [Squad](https://github.com/bradygaster/squad) AI-agent team as a first-class .NET Aspire resource.

## Getting Started

### Install the package

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Squad
```

### Add a Squad team to your AppHost

```csharp
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Register a Squad team as a logical Aspire resource.
// teamRoot is the directory that contains the `.squad/` folder.
var research = builder.AddSquad("research-squad",
    teamRoot: @"C:\repos\my-research-team");

// Wire the team into a downstream service project.
// The service receives a `squad://...` connection string describing the team.
builder.AddProject<Projects.MyApi>("api")
    .WithReference(research)
    .WithHttpEndpoint(name: "http", env: "HTTP_PORTS");

builder.Build().Run();
```

## What it does

`AddSquad(name, teamRoot)` registers a `SquadResource` that:

- Reads the team roster from `{teamRoot}/.squad/team.md`
- Surfaces the roster, team root, and protocol version as dashboard properties
- Implements `IResourceWithConnectionString` so downstream services can `.WithReference(squad)` and receive a `squad://resource/{name}?teamRoot={...}&agents={csv}&protocol=maf-1.0` descriptor

The resource is logical — it does not start a listener by itself. Service projects that reference the squad can use the connection string to instantiate a [`Squad.Agents.AI`](https://www.nuget.org/packages/Squad.Agents.AI) `AIAgent` (Microsoft Agent Framework adapter) that orchestrates the referenced team.

## Roster discovery

The integration parses `.squad/team.md` looking for either of:

```markdown
| Ralph     | Work Monitor   | ... |
- **Ralph** (Work Monitor)
```

Only names whose lowercase form maps to an existing `.squad/agents/{name}/charter.md` are kept. If `team.md` does not exist, a sensible default roster is returned so the resource is still useful.

## Feedback & contributing

Issues and PRs welcome at [CommunityToolkit/Aspire](https://github.com/CommunityToolkit/Aspire).
