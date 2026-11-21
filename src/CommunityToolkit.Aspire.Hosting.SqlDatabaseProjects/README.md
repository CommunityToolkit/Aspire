# CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects library

This package provides [Aspire](https://aspire.dev/get-started/what-is-aspire/) integration for SQL Server Database Projects. It allows you to publish SQL Database Projects as part of your Aspire AppHost projects. It currently works with both [MSBuild.Sdk.SqlProj](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj) and [Microsoft.Build.Sql](https://github.com/microsoft/DacFx) based projects.

## Usage

To use this package, install it into your Aspire AppHost project:

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects
```

SQL schemas can be supplied as a project path, a NuGet package ID and version, generated metadata from an AppHost reference, or a DACPAC file. Choose the form that fits how your application obtains its database artifacts.

### SQL projects by path

Specify a local SQL project path without adding a SQL `ProjectReference` to the AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var database = builder.AddSqlServer("sql").AddDatabase("database");

builder.AddSqlProject("schema", "../Database/Database.sqlproj", configuration: "Release")
       .WithReference(database);

builder.Build().Run();
```

Relative project paths are resolved against the AppHost directory. Relative DACPAC output paths (`SqlTargetPath`, falling back to `TargetPath`) are resolved against the SQL project directory. The configuration defaults to `Debug` and is used for both `dotnet build` and output evaluation. Each deployment request builds the project, including its restore, before resolving the DACPAC. Both `Microsoft.Build.Sql` and `MSBuild.Sdk.SqlProj` are supported; SSDT projects requiring other toolchains are not supported by this path-based workflow. The required .NET SDK must already be installed.

### NuGet packages by ID and version

Specify an exact package version without adding a SQL `PackageReference` to the AppHost:

```csharp
builder.AddSqlPackage("package-schema", "Company.Database", "1.2.3-preview.1")
       .WithReference(database);

builder.AddSqlPackage("custom-schema", "Company.OtherDatabase", "2.0.0")
       .WithDacpac("schema/custom.dacpac")
       .WithReference(database);
```

The version must be exact; version ranges and floating versions are not accepted. The default DACPAC location inside the package is `tools/<PackageId>.dacpac`. For this overload only, `WithDacpac` specifies a path relative to the package root; absolute paths and paths outside that root are not supported. The original package-relative input is preserved across repeated deployments.

The integration runs `dotnet restore` with an isolated temporary `PackageDownload` project under the AppHost's `obj` directory. The project targets the AppHost's .NET version and uses its SDK selection and NuGet configuration hierarchy, the configured package cache (including `NUGET_PACKAGES`), and credential providers available in the environment. It does not add SQL package references to the AppHost or copy credentials into generated files. Package dependencies are not restored automatically: the downloaded package must contain the files needed to deploy its DACPAC.

For project paths and package IDs, build and restore run only when deployment is requested, not during resource registration. Failures or cancellation prevent deployment. `WithExplicitStart()` defers preparation and deployment until the dashboard's Deploy command is invoked. `WithSkipWhenDeployed()` still prepares the artifact before checking its checksum. SQL project and package resources are excluded from published manifests. Cloud deployment and file watching are not supported.

### Referenced SQL projects

An AppHost project reference makes generated project metadata available to `AddSqlProject<TProject>`. Add a reference to the SQL project:

```bash
dotnet add reference ../MySqlProj/MySqlProj.csproj
```

> Note: Depending on the project reference metadata, a SQL project reference may produce warning ASPIRE004. The path-based API above does not require that reference.

Finally add the project as a resource to your Aspire AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddSqlProject<Projects.MySqlProj>("mysqlproj")
       .WithReference(sql);

builder.Build().Run();
```

Now when you run your Aspire AppHost project you will see the SQL Database Project being published to the specified SQL Server.

### Referenced NuGet packages

A package reference marked with `IsAspirePackageResource` provides generated metadata for `AddSqlPackage<TPackage>`:

```xml
<PackageReference Include="Company.Database" Version="1.2.3" IsAspirePackageResource="true" />
```

```csharp
builder.AddSqlPackage<Packages.Company_Database>("package-schema")
       .WithReference(database);
```

With this form, the AppHost's NuGet restore acquires the package. `WithDacpac` can specify a package-relative DACPAC path instead of the default `tools/<PackageId>.dacpac`.

## Local .dacpac file support

To deploy an available DACPAC file, specify its path directly. File preparation is managed outside this resource:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddSqlProject("mysqlproj")
       .WithDacpac("path/to/mysqlproj.dacpac")
       .WithReference(sql);

builder.Build().Run();
```

## DACPAC pipeline artifacts

SQL project resources automatically register the DACPAC pipeline steps. Manual report, script, and deploy operations use the DACPAC already available at the configured SQL project output path by default and do not rebuild it. Select `DacpacSource.Build` when a build is required, or include `publish-<resource>`/`deploy-<resource>` in the aggregate command; aggregate inclusion builds in `Release` configuration by default. `publish-<resource>` copies the resulting DACPAC to `<pipeline-output>/<resource>/<resource>.dacpac`, while the build step leaves a stable CI artifact at `<pipeline-output>/build/<resource>/<resource>.dacpac`.

The report, script, and deploy pipeline steps are opt-in operations and do not run as part of `aspire publish` unless configured. To include DACPAC export in the aggregate publish pipeline:

```csharp
builder.AddSqlProject("schema", "../Database/Database.sqlproj", configuration: "Debug")
    .WithPipelineOptions(new()
    {
        IncludeInPublish = true,
        BuildConfiguration = "Release"
    });
```

`WithPipelineOptions` is optional. The pipeline always registers `publish-schema` as a manually invocable step; `aspire publish` includes it only with `IncludeInPublish`.

To use a DACPAC that already exists at the project's configured output, no source option is required. To explicitly build during a manual operation, set `Source = DacpacSource.Build`. To consume an externally produced artifact, set `Source = DacpacSource.File` and provide the `<resource>-dacpac-path` parameter.

For split CI jobs, select the file source through pipeline configuration and provide an Aspire parameter containing the downloaded DACPAC path:

```powershell
$env:DacpacPipeline__schema__Source = "File"
$env:Parameters__schema_dacpac_path = (
    Get-Item -LiteralPath ".\artifacts\Database.dacpac" -ErrorAction Stop
).FullName
aspire do publish-schema
```

In this mode the SQL project is not evaluated or rebuilt. A missing or invalid path fails the selected step. SQL packages by ID/version or by package metadata continue to resolve their DACPAC through the package rather than through `DacpacSource`.

To inspect schema changes without modifying the target, call `aspire do report-schema` for an XML deployment report or `aspire do script-schema` for a generated T-SQL deployment script. Both operations resolve the target connection only after `ProcessParameters` and write to `<pipeline-output>/artifacts/dacpac/schema/` by default. They do not create or deploy the database. Use `aspire do deploy-schema` to apply that DACPAC to the referenced target. The pipeline deploy is independent from `aspire run` and does not update the local `WithSkipWhenDeployed` checksum.

Set `IncludeInDeploy = true` in `WithPipelineOptions` to make `aspire deploy` include the resource-specific deploy step. Aggregate deploys wait for pipeline steps tagged `ProvisionInfrastructure`, honor `WaitFor`/`WaitForCompletion` relationships between SQL project resources, and serialize opted-in schema deployments that target the same database. Steps targeting different databases can run in parallel.

The same pipeline options are available in TypeScript:

```typescript
await databaseProject.withPipelineOptions({
    includeInPublish: true,
    includeInDeploy: true,
});
```

Nullable DTO values are pointers in generated Go APIs:

```go
includeInPublish := true
includeInDeploy := true
project.WithPipelineOptions(aspire.PipelineOptionsOptions(&aspire.DacpacPipelineOptions{
    IncludeInPublish: &includeInPublish,
    IncludeInDeploy:  &includeInDeploy,
}))
```

Pipeline operations use `dotnet dnx Microsoft.SqlPackage@170.3.93` by default. The connection string is written to a temporary, access-restricted publish profile and is not passed on the SqlPackage command line. Log redaction is best effort; diagnostics and external tooling may still expose sensitive values.

`BlockOnPossibleDataLoss` defaults to `True` when a publish profile does not specify it. Set it explicitly in the publish profile only after reviewing `report-schema` and the generated deployment script.

Pipeline mode accepts `WithDacDeployOptions(path)` publish profiles and preserves their deployment properties and SQLCMD variables. A `WithConfigureDacDeployOptions(...)` callback cannot be serialized for SqlPackage and is rejected by report, script, and deploy operations with a clear error. The callback remains supported for local DacFx deployment. When a local `SqlServerDatabaseResource` is referenced in pipeline mode, the integration registers a secret `<database-resource>-connection-string` parameter instead of attempting to resolve the container endpoint. Supply it through `Parameters__<database-resource>_connection_string` (with dashes represented by underscores).

The DACPAC pipeline APIs are experimental and may change. See the `CTASPIREPIPELINES001` diagnostic when compiling against them.

## Support for existing SQL Server

Instead of using the `AddSqlServer` method to use a SQL Server container, you can specify a connection string to an existing server:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Get an existing connection string from the configuration
var connection = builder.AddConnectionString("Aspire");

builder.AddSqlProject<Projects.SdkProject>("mysqlproj")
       .WithReference(connection);

builder.Build().Run();
```

The referenced connection string should include the target database name using either the `Database` or `Initial Catalog` setting so the deployment target can be inferred.

## Deployment options support

Define options that affect the behavior of package deployment.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddSqlProject("mysqlproj")
       .WithConfigureDacDeployOptions(options => options.IncludeCompositeObjects = true)
       .WithReference(sql);

builder.Build().Run();
```

## Ability to skip deployment

You can use the `WithSkipWhenDeployed` method to avoid re-deploying your SQL Database Project if no changes have been made. This is useful in scenarios where the SQL container database is persisted to permanent disk and will significantly improve the Aspire AppHost project startup time.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddSqlServer("sql")
    .WithDataVolume("testdata")
    .WithLifetime(ContainerLifetime.Persistent);

var database = server.AddDatabase("test");

var sdkProject = builder.AddSqlProject<Projects.SdkProject>("mysqlproj")
    .WithSkipWhenDeployed()
    .WithReference(database);

builder.Build().Run();
```
