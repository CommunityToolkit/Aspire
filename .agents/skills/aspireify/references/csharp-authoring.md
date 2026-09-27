# C# AppHost Authoring

Patterns for editing C# AppHosts — both SDK-style (`.csproj` + `Program.cs`) and
file-based (`apphost.cs` with `#:sdk` / `#:package` directives).

> Look up unfamiliar API: `aspire docs api search <query> --language csharp`
> then `aspire docs api get <id>`. Don't guess overloads.

## SDK-style (.csproj)

`Program.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var pg = builder.AddPostgres("pg").AddDatabase("appdb");
var cache = builder.AddRedis("cache");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(pg)
    .WithReference(cache)
    .WaitFor(pg)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Worker>("worker")
    .WithReference(pg)
    .WaitFor(pg);

builder.AddNextJsApp("web", "../web")
    .WithReference(api)
    .WaitFor(api)
    .WithBrowserLogs();

builder.Build().Run();
```

The `Projects.X` strongly-typed reference comes from the SDK — `<Sdk Name="Aspire.AppHost.Sdk" />`
in `.csproj` and `<ProjectReference>` for each project.

New 13.5 AppHosts set `<AspireUseCliBundle>true</AspireUseCliBundle>`. Preserve that
property; for existing AppHosts, add it only when the user chooses to opt into CLI-owned
DCP/dashboard versions.

For projects not in the SDK references, use the path overload:

```csharp
builder.AddProject("api", "../Api/Api.csproj")
    .WithReference(pg);
```

## File-based AppHost (apphost.cs)

Top of file uses `#:sdk` and `#:package` directives — no `.csproj` required:

Replace the version placeholders with package versions compatible with the AppHost's
selected Aspire release.

```csharp
#:sdk Aspire.AppHost.Sdk
#:property AspireUseCliBundle=true
#:package Aspire.Hosting.PostgreSQL@<postgres-package-version>
#:package Aspire.Hosting.Redis@<redis-package-version>
#:package Aspire.Hosting.JavaScript@<javascript-package-version>

var builder = DistributedApplication.CreateBuilder(args);

var pg = builder.AddPostgres("pg").AddDatabase("appdb");
var cache = builder.AddRedis("cache");

var api = builder.AddProject("api", "../Api/Api.csproj")
    .WithReference(pg)
    .WithReference(cache)
    .WithExternalHttpEndpoints();

builder.AddNextJsApp("web", "../web")
    .WithReference(api)
    .WithBrowserLogs();

builder.Build().Run();
```

In file-based mode you cannot use `Projects.X` strongly-typed references — use
the path overload of `AddProject`.

## Common Builder Methods

| Method | Purpose |
|--------|---------|
| `AddProject<T>(name)` / `AddProject(name, path)` | Add a .NET project |
| `AddContainer(name, image)` | Add a container |
| `AddDockerfile(name, contextPath)` | Build from a Dockerfile |
| `AddNodeApp(name, scriptPath)` | Plain Node service |
| `AddNextJsApp(name, projectPath)` | Next.js with auto standalone publish |
| `AddViteApp(name, projectPath)` | Vite app — pair with `PublishAsStaticWebsite` |
| `AddPostgres` / `AddRedis` / `AddMongoDB` / `AddRabbitMQ` / `AddSqlServer` / `AddMySql` / `AddKafka` | Datastores & messaging |
| `AddAzureCosmosDB` / `AddAzureServiceBus` / `AddAzureRedis` / `AddAzureStorage` / `AddAzureSqlServer` | Azure resources |
| `WithReference(other)` | Inject connection string / endpoints |
| `WaitFor(other)` | Block start until target is ready |
| `WithEnvironment("KEY", value)` | Add env var (any value type — endpoint, parameter, expression) |
| `WithExternalHttpEndpoints()` | Mark HTTP endpoints as externally reachable |
| `WithEndpoint(name, e => …)` | Add or **update** existing endpoints |
| `WithHttpHealthCheck(path)` | Wire health check used by `WaitFor` |

## Endpoints

`WithEndpoint` updates the existing endpoint when called twice with the same name
(rather than throwing). Use this to layer endpoint config across helpers.

```csharp
var api = builder.AddProject<Projects.Api>("api")
    .WithEndpoint("admin", e => e.Port = 9000)
    .WithEndpoint("admin", e => e.ExcludeReferenceEndpoint = true);
//                                ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
// Admin endpoint is NOT injected into consumers via WithReference().
```

## Compute Environments

When multiple environments are declared, every resource **must** explicitly bind
to one with `.WithComputeEnvironment(env)`.

```csharp
var aca = builder.AddAzureContainerAppEnvironment("aca");
var aks = builder.AddAzureKubernetesEnvironment("aks")
    .WithSystemNodePool("Standard_D2s_v5", minCount: 1, maxCount: 3);

builder.AddProject<Projects.Api>("api").WithComputeEnvironment(aca);
builder.AddProject<Projects.Worker>("worker").WithComputeEnvironment(aks);
```

Plain Kubernetes (Helm-based): `AddKubernetesEnvironment("k8s")`.

## JavaScript Publish Helpers

| Helper | Use For |
|--------|---------|
| `PublishAsStaticWebsite(apiPath, apiTarget)` | Vite SPA → YARP-served static site, optional API reverse-proxy |
| `PublishAsNodeServer(entryPoint, outputPath)` | Pre-bundled Node server (TanStack Start, SvelteKit) |
| `PublishAsPackageScript(scriptName)` | package-manager `start` / `serve` runtime (full Nitro Next.js, Remix, Astro SSR) |

```csharp
#pragma warning disable ASPIREJAVASCRIPT001
builder.AddViteApp("web", "../web")
    .WithReference(api)
    .PublishAsStaticWebsite(apiPath: "/api", apiTarget: api);
#pragma warning restore ASPIREJAVASCRIPT001
```

`AddNextJsApp` auto-applies standalone publishing — no explicit `PublishAs*`
needed. Set `output: "standalone"` in `next.config.js`.

## Browser Logs

```csharp
builder.AddViteApp("frontend", "../frontend")
    .WithBrowserLogs();   // Aspire.Hosting.Browsers — adds console + screenshots to dashboard
```

## Azure-Specific

| API | Purpose |
|-----|---------|
| `AddAzureFrontDoor("frontdoor").WithOrigin(api).WithOrigin(web)` | Global edge/CDN — provisions endpoint, origin group, origin, route per `WithOrigin` |
| `AddNetworkSecurityPerimeter("nsp").WithAccessRule(...)` + `resource.WithNetworkSecurityPerimeter(nsp)` | NSP for Storage / Key Vault / Cosmos / SQL |
| `AddAzureKubernetesEnvironment("aks").WithSystemNodePool(sku, minCount, maxCount)` | AKS hosting |
| `AddPromptAgent(...)` | Azure AI Foundry Prompt Agent (replaces non-functional `AddAndPublishPromptAgent`) |
| `resource.WithPrivateEndpoint()` | Now supported on ACR, Azure OpenAI, AI Foundry |

## Lifecycle Hooks

```csharp
builder.SubscribeBeforeStart(async e => { /* runs before resources start */ });
builder.SubscribeAfterResourcesCreated(async e => { /* runs after creation */ });
```

## HTTP Commands

```csharp
builder.AddProject<Projects.Api>("api")
    .WithHttpCommand("/admin/sync", "Sync now", commandOptions: new()
    {
        ResultMode = HttpCommandResultMode.Auto  // None | Auto | Json | Text
    });
```

`HttpCommandResultMode` returns the response body to the dashboard's
notification center.

## Resource Command Arguments And Interactions

Use `CommandOptions.Arguments` for input that must work from both the dashboard and CLI.
Read values from `ExecuteCommandContext.Arguments`. Hosting callback contexts expose
`Services`; `ServiceProvider` is obsolete in 13.5.

```csharp
using Aspire.Hosting.ApplicationModel;

builder.AddProject<Projects.Api>("api")
    .WithCommand(
        "echo",
        "Echo",
        context =>
        {
            var message = context.Arguments.GetString("message");
            return Task.FromResult(CommandResults.Success(message));
        },
        commandOptions: new CommandOptions
        {
            Arguments =
            [
                new InteractionInput
                {
                    Name = "message",
                    InputType = InputType.Text,
                    Required = true,
                },
            ],
        });
```

Direct Interaction Service prompts require an attached UI. Resolve
`IInteractionService` from `context.Services`, check `IsAvailable`, and provide a
noninteractive path before calling prompt APIs.

## Experimental Terminals

`WithTerminal()` exposes an interactive dashboard/CLI terminal. Suppress
`ASPIRETERMINAL001`, keep `Columns` and `Rows` positive, and do not generate the removed
`TerminalOptions.Shell` property:

```csharp
#pragma warning disable ASPIRETERMINAL001
builder.AddContainer("repl", "my-repl")
    .WithTerminal(options =>
    {
        options.Columns = 120;
        options.Rows = 30;
    });
```
