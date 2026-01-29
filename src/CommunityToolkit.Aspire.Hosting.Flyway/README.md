# CommunityToolkit.Aspire.Hosting.Flyway

An Aspire hosting integration for [Flyway](https://flywaydb.org/), a database migration tool that helps manage and automate database schema changes.

> This integration is meant to be used in conjunction with a database resource, such as PostgreSQL, and the Flyway extension built for that database resource.
> It is also meant to be used by integration developers who want to add Flyway support to more database resources.

## Getting started

### Prerequisites

- .NET 8.0 or later
- Docker (for running the Flyway and database containers)

### Installation

Install the package by adding a PackageReference to your `AppHost` project:

```xml
<PackageReference Include="CommunityToolkit.Aspire.Hosting.Flyway" />
```

Or to your file-based `AppHost`:

```csharp
#:package CommunityToolkit.Aspire.Hosting.Flyway@13.*
```

### Usage

In your `AppHost` project, call the `AddFlyway` method to add Flyway to your application with a migration scripts directory:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var flyway = builder.AddFlyway("flyway", "./migrations");

// The rest of AppHost

builder.Build().Run();
```

The `migrationScriptsPath` parameter specifies the path to your migration scripts on the host machine, which will be mounted into the Flyway container.

## Feedback & contributing

This is an early version of the Flyway integration. It is production-ready, but not yet feature complete.
If you have any suggestions for features or improvements, please open an issue or a pull request on the [GitHub repository](https://github.com/CommunityToolkit/Aspire).
We welcome feedback and contributions.
