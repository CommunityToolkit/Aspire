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

Relative project paths are resolved against the AppHost directory. The configuration defaults to `Debug` and is used for both `dotnet build` and output evaluation (`SqlTargetPath`, falling back to `TargetPath`). Each deployment request builds the project, including its restore, before resolving the DACPAC. Both `Microsoft.Build.Sql` and `MSBuild.Sdk.SqlProj` are supported; SSDT projects requiring other toolchains are not supported by this path-based workflow. The required .NET SDK must already be installed.

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

The integration runs `dotnet restore` with an isolated temporary `PackageDownload` project under the AppHost's `obj` directory. It uses the AppHost's SDK selection and NuGet configuration hierarchy, the configured package cache (including `NUGET_PACKAGES`), and credential providers available in the environment. It does not add SQL package references to the AppHost or copy credentials into generated files. Package dependencies are not restored automatically: the downloaded package must contain the files needed to deploy its DACPAC.

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
