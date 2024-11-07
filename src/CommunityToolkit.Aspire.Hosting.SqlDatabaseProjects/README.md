# CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects library
This package provides [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) integration for SQL Server Database Projects. It allows you to publish SQL Database Projects as part of your .NET Aspire AppHost projects. It currently works with both [MSBuild.Sdk.SqlProj](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj) and [Microsoft.Build.Sql](https://github.com/microsoft/DacFx) (aka .sqlprojx) based projects.

## Usage
To use this package, install it into your .NET Aspire AppHost project:

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects
```

Next, add a reference to the MSBuild.Sdk.SqlProj or Microsoft.Build.Sql project you want to publish in your .NET Aspire AppHost project:

```bash
dotnet add reference ../MySqlProj/MySqlProj.csproj
```

> Note: Adding this reference will currently result in warning ASPIRE004. This is a known issue and will be resolved in a future release.

Finally add the project as a resource to your .NET Aspire AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddSqlProject<Projects.MySqlProj>("mysqlproj")
       .WithReference(sql);

builder.Build().Run();
```

Now when you run your .NET Aspire AppHost project you will see the SQL Database Project being published to the specified SQL Server.

## Local .dacpac file support
If you are sourcing your .dacpac file from somewhere other than a project reference, you can also specify the path to the .dacpac file directly:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .AddDatabase("test");

builder.AddSqlProject("mysqlproj")
       .WithDacpac("path/to/mysqlproj.dacpac")
       .WithReference(sql);

builder.Build().Run();
```