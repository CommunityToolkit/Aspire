# CommunityToolkit.Aspire.Hosting.Neon library

Provides extension methods and resource definitions for using Neon Postgres with a .NET Aspire AppHost.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```shell
dotnet add package CommunityToolkit.Aspire.Hosting.Neon
```

### Example usage

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var neonApiKey = builder.AddParameter("neon-api-key", "your-key", secret: true);

var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddBranch("dev");

var database = neon.AddDatabase("appdb", "appdb");

builder.AddProject<Projects.Api>("api")
    .WithReference(database);

builder.Build().Run();
```

## Provisioning mode

Neon infers provisioning behavior from your fluent configuration:

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddBranch("dev");
```

In this example, Neon uses `Provision` mode because project/branch creation is requested.
For existing-only workflows, use `AsExisting()`.

Neon creates a real provisioner resource (`{neonResourceName}-provisioner`) that performs a one-shot execution and exits.

Neon wires the one-shot provisioner automatically and uses inferred mode based on current options.
The provisioner project template is bundled with the hosting package and materialized at runtime under:

- run mode: `.aspire/cache/neon/{appHostHash}/neon-provisioner`
- publish mode: `.aspire/cache/neon/{appHostHash}/neon-provisioner`

The provisioner writes startup output to a deterministic path:

- run mode (host execution): `%TEMP%/aspire-neon-output/{appHostHash}/{runOutputDiscriminator}/{neonResourceName}.json`
- publish/deploy mode (container execution): `/neon-output/{neonResourceName}.json`

In publish mode, the provisioner uses a named Docker volume (`{neonResourceName}-neon-output`) mounted at `/neon-output`. Consumer containers mount the same volume (read-only) to access the provisioner output. The provisioner also writes per-database `.env` files alongside the JSON output (e.g., `/neon-output/appdb.env`).

## Extension methods

### Project configuration

| Method | Purpose |
| --- | --- |
| `WithProjectId(id)` | Use an existing project by ID. |
| `WithProjectName(name)` | Resolve an existing project by name. |
| `AddProject(name, ...)` | Create the project if it does not exist. |
| `AsExisting()` | Use existing project/branch/endpoint resources only. |
| `ConfigureInfrastructure(Action<NeonProjectOptions>)` | Configure all infrastructure options via callback. |
| `GetProvisionerBuilder()` | Get the provisioner `IResourceBuilder<ProjectResource>` to call Aspire SDK methods directly. |

### Organization

| Method | Purpose |
| --- | --- |
| `WithOrganizationId(id)` | Target a specific organization by ID. |
| `WithOrganizationName(name)` | Resolve an organization by name. |

### Branch configuration

| Method | Purpose |
| --- | --- |
| `WithBranchId(id)` | Use an existing branch by ID. |
| `WithBranchName(name)` | Resolve an existing branch by name. |
| `AddBranch(name, endpointType?)` | Create the branch if it does not exist. |
| `AddEphemeralBranch(prefix?, endpointType?)` | Create a disposable branch for each run. |
| `WithBranchRestore(Action<NeonBranchRestoreOptions>?)` | Restore (refresh) the branch from a source. |
| `WithAnonymizedData(Action<NeonAnonymizationOptions>)` | Create an anonymized branch with masking rules. |
| `AsDefaultBranch()` | Set the resolved branch as the project default. |

### Connection

| Method | Purpose |
| --- | --- |
| `WithDatabaseName(name)` | Override the default database name. |
| `WithRoleName(name)` | Override the default role name. |
| `WithConnectionPooler()` | Route connections through the connection pooler. |

## Provisioning behavior

- Provisioner mode is inferred from configuration.
- `AsExisting` forces existing-only attach semantics.
- AppHost startup is external-only: Neon reads the provisioner output file instead of performing startup-time Neon API resource resolution.

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddBranch("dev");

var database = neon.AddDatabase("appdb", "appdb");

builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(neon);
```

Neon forwards configuration as environment variables to the provisioner resource and instructs the provisioner to emit a startup output artifact consumed by the Neon resource.

External provisioner environment contract:

| Variable | Purpose |
| --- | --- |
| `NEON_API_KEY` | Neon API key (required). |
| `NEON_MODE`, `NEON_OUTPUT_FILE_PATH` | Provisioner mode (`attach`/`provision`) and output artifact path. |
| `NEON_PROJECT_ID`, `NEON_PROJECT_NAME`, `NEON_CREATE_PROJECT_IF_MISSING` | Project attach/create behavior. |
| `NEON_REGION_ID`, `NEON_POSTGRES_VERSION`, `NEON_ORGANIZATION_ID`, `NEON_ORGANIZATION_NAME` | Project creation scope. |
| `NEON_BRANCH_ID`, `NEON_BRANCH_NAME`, `NEON_PARENT_BRANCH_ID`, `NEON_PARENT_BRANCH_NAME` | Branch attach/create parent selection. |
| `NEON_BRANCH_INIT_SOURCE`, `NEON_BRANCH_PROTECTED`, `NEON_BRANCH_EXPIRES_AT`, `NEON_BRANCH_PARENT_LSN`, `NEON_BRANCH_PARENT_TIMESTAMP`, `NEON_BRANCH_ARCHIVED` | Branch creation options. |
| `NEON_CREATE_BRANCH_IF_MISSING`, `NEON_BRANCH_SET_AS_DEFAULT`, `NEON_USE_EPHEMERAL_BRANCH`, `NEON_EPHEMERAL_BRANCH_PREFIX` | Branch creation/default/ephemeral behavior. |
| `NEON_BRANCH_RESTORE_ENABLED`, `NEON_BRANCH_RESTORE_SOURCE_BRANCH_ID`, `NEON_BRANCH_RESTORE_SOURCE_LSN`, `NEON_BRANCH_RESTORE_SOURCE_TIMESTAMP`, `NEON_BRANCH_RESTORE_PRESERVE_UNDER_NAME` | Branch restore behavior. |
| `NEON_BRANCH_ANONYMIZATION_ENABLED`, `NEON_BRANCH_ANONYMIZATION_START`, `NEON_BRANCH_MASKING_RULES_JSON` | Anonymized branch behavior and masking rules. |
| `NEON_ENDPOINT_ID`, `NEON_ENDPOINT_TYPE`, `NEON_CREATE_ENDPOINT_IF_MISSING` | Endpoint attach/create behavior. |
| `NEON_DATABASE_NAME`, `NEON_ROLE_NAME`, `NEON_USE_CONNECTION_POOLER`, `NEON_DATABASE_SPECS_JSON` | Database/role and connection output behavior. |

You can configure these settings using `ConfigureInfrastructure` or the fluent extensions shown above. If you use both, the last value written wins. If you already have a project ID, prefer `WithProjectId`.

### Organization example

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .WithOrganizationName("my-org");
```

## Ephemeral branches

Ephemeral branches are a toolkit concept (not a Neon feature) that creates a fresh branch for each app run and deletes it on shutdown. This is useful for disposable test environments and integration testing.

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddEphemeralBranch("aspire-");
```

When enabled, the provisioner deletes existing branches with the configured prefix before creating a new ephemeral branch. Ephemeral branches are created with a 1-day expiration so Neon can automatically clean up lingering branches.

### Dashboard commands

The integration includes **Suspend** and **Resume** commands in the Aspire dashboard. These let you pause the Neon compute endpoint to save resources and restart it when needed, without restarting the AppHost.

Suspend/Resume run through the provisioner command path and require the Neon resource to be provisioned/attached first (resolved project and endpoint IDs).

## Resource lifecycle

The Neon resource follows a health-check–based lifecycle that integrates with Aspire's `WaitFor()`:

| State | Meaning |
| --- | --- |
| **Starting** | Resource created, provisioning not yet begun. |
| **Connecting** | API key validated; connecting to the Neon API. |
| **Running** | All provisioning (project, branch, database, endpoint) is complete. |
| **Healthy** | Health check confirms the connection string is available. Dependent resources that use `WaitFor()` are unblocked at this point. |

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddBranch("dev");

var database = neon.AddDatabase("appdb", "appdb");

builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(neon);
```

## Consuming connection strings in dependent projects

When a dependent project uses `.WithReference(database)`, the connection string is made available using the database resource name as the connection name.

### Run mode

In run mode, `.WithReference(database)` delegates to the standard Aspire `WithReference` which injects the connection string as `ConnectionStrings__{name}` once the provisioner finishes. No additional setup is needed in the consuming project:

```csharp
var database = neon.AddDatabase("appdb", "appdb");

builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(neon);
```

In the consuming project, read the connection string using standard configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("appdb")
    ?? throw new InvalidOperationException("Connection string 'appdb' was not found.");

builder.Services.AddNpgsqlDataSource(connectionString);
```

### Publish mode (Docker Compose, ACA, etc.)

In publish mode, the provisioner runs as a container and writes per-database `.env` files to a shared Docker volume. `.WithReference(database)` handles the wiring differently based on the consumer type:

**Container consumers** (e.g., `AddContainer`): The container entrypoint is automatically replaced with a shell wrapper that reads the `.env` file, extracts the connection URI, and exports it as `ConnectionStrings__{name}` before starting the original command. No code changes needed.

**Project consumers** (e.g., `AddProject`): The container cannot have its entrypoint overridden from the hosting layer. Instead, helper environment variables (`NEON_OUTPUT_DIR` and `NEON_ENV_FILE__{name}`) are injected. The consumer application must read the `.env` file at startup using the client package:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Resolves connection strings from Neon provisioner .env files.
// Call this before any service that reads ConnectionStrings.
builder.AddNeonConnectionStrings();

string connectionString = builder.Configuration.GetConnectionString("appdb")
    ?? throw new InvalidOperationException("Connection string 'appdb' was not found.");

builder.Services.AddNpgsqlDataSource(connectionString);
```

Install the client package in your service project:

```shell
dotnet add package CommunityToolkit.Aspire.Neon
```

> **Note:** If you use `AddNeonClient("appdb")`, the env-file resolution happens automatically — you do not need to call `AddNeonConnectionStrings` separately.

### Entity Framework Core

Entity Framework Core works with both run and publish modes:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Required in publish mode for project consumers:
builder.AddNeonConnectionStrings();

string connectionString = builder.Configuration.GetConnectionString("appdb")
    ?? throw new InvalidOperationException("Connection string 'appdb' was not found.");

builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseNpgsql(connectionString));
```

## Branch restore (refresh)

You can restore a branch to a point-in-time state from another branch using the Neon branch restore API:

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .WithProjectId("my-project-id")
    .WithBranchName("staging")
    .WithBranchRestore(restore =>
    {
        restore.SourceBranchId = "br-main-abc123";
        restore.PreserveUnderName = "staging-backup";
    });
```

When `WithBranchRestore` is called, the branch is restored after it has been resolved. The default behavior refreshes from the source branch's latest state. You can optionally specify `SourceLsn` or `SourceTimestamp` for point-in-time recovery, and `PreserveUnderName` to keep the old state under a new branch name.

## Anonymized branches

You can create branches with anonymized data using Neon's masking rules:

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddBranch("anonymized-dev")
    .WithAnonymizedData(anon =>
    {
        anon.MaskingRules.Add(new NeonMaskingRule
        {
            DatabaseName = "appdb",
            TableName = "users",
            ColumnName = "email",
            MaskingFunction = "mask_email"
        });
    });
```

When anonymization is enabled, the toolkit uses the `branch_anonymized` API endpoint instead of the standard branch creation endpoint. This creates a branch with masked data based on the configured rules.

## Database naming guidance

Use `AddDatabase` to register specific databases your app depends on. Use `WithDatabaseName` to set the default database name only when you plan to reference the Neon project resource directly (without adding individual databases). In most cases, prefer `AddDatabase` for clarity and explicit dependencies.

## Additional Information

- [Neon Docs](https://neon.tech/docs)
- [Neon API Reference](https://api-docs.neon.tech/reference/getting-started-with-neon-api)

## Feedback & contributing

[CommunityToolkit/Aspire](https://github.com/CommunityToolkit/Aspire)

## Local vs production configuration

Use conditional configuration so local runs can create or use disposable branches, while production uses fixed IDs and skips creation.

### Local development (publish-mode aware)

```csharp
IResourceBuilder<NeonProjectResource> neon;

if (!builder.ExecutionContext.IsPublishMode)
{
    neon = builder.AddNeon("neon", neonApiKey)
        .AddProject("aspire-neon")
        .AddEphemeralBranch("aspire-");
}
else
{
    neon = builder.AddNeon("neon", neonApiKey)
        .WithProjectId(builder.Configuration["Neon:ProjectId"]!)
        .WithBranchId(builder.Configuration["Neon:BranchId"]!)
        .AsExisting();
}
```

This configuration allows local runs to create the project if needed and creates an ephemeral branch that is cleaned up on shutdown or the next run.

### Production deployment (publish-mode aware)

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .WithProjectId(builder.Configuration["Neon:ProjectId"]!)
    .WithBranchId(builder.Configuration["Neon:BranchId"]!)
    .AsExisting();
```

In production, prefer explicit IDs and disable creation so deployments are deterministic. Provide `Neon:ProjectId` and optionally `Neon:BranchId` via configuration or environment variables.

To ensure dependent projects only start after provisioner work is done and connection info is healthy, use `.WaitFor(neon)` on those dependents:

```csharp
var database = neon.AddDatabase("appdb", "appdb");

builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(neon);
```

## Optional client package

If you want a convenience registration for `NpgsqlDataSource`, you can use `CommunityToolkit.Aspire.Neon` in consuming services:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddNeonClient("appdb");
```

This is optional. The hosting integration already provides connection information to dependents via `.WithReference(...)`.

For publish mode with project consumers, you can also use `AddNeonConnectionStrings()` to resolve all provisioned connection strings without registering `NpgsqlDataSource`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddNeonConnectionStrings(); // makes all Neon connection strings available
```

## WaitFor guidance

In **run mode**, add `.WaitFor(neon)` to ensure the consumer waits until Neon provisioning is complete and the health check passes:

```csharp
builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(neon);
```

You can also use `.WaitFor(database)` instead — both the `NeonProjectResource` and `NeonDatabaseResource` support `IResourceWithWaitSupport`.

In **publish mode**, `.WithReference(database)` already handles waiting internally — it adds `WaitForCompletion` on the provisioner container and `WaitFor` on the database resource. An additional `.WaitFor(neon)` is not required but is harmless.

## Accessing the provisioner resource

The provisioner resource is created automatically by `AddNeon`. You can access it directly if needed:

```csharp
var neon = builder.AddNeon("neon", neonApiKey)
    .AddProject("aspire-neon")
    .AddBranch("dev");

// Access the provisioner project resource for advanced configuration.
var provisionerResource = neon.Resource.ProvisionerResource;

// Or use GetProvisionerBuilder() to get an IResourceBuilder for fluent Aspire calls.
var provisionerBuilder = neon.GetProvisionerBuilder();
```


