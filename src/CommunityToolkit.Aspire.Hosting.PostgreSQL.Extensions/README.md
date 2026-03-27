# CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions library

This integration contains extensions for the [PostgreSQL hosting package](https://nuget.org/packages/Aspire.Hosting.PostgreSQL) for Aspire.

The integration provides support for running:

* [DbGate](https://github.com/dbgate/dbgate) and [Adminer](https://github.com/vrana/adminer) to interact with the PostgreSQL database.
* [Flyway](https://github.com/flyway/flyway/) for database migrations.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```shell
dotnet add package CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions
```

### Example usage

#### Adminer and DbGate

In the _Program.cs_ file of `AppHost`, define an Postgres resource, then call `AddPostgres`:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithDbGate()
    .WithAdminer();
```

#### Flyway

In the `AppHost` file, define a Flyway resource and link it to a Postgres database resource:

```csharp
var flyway = builder.AddFlyway("flyway", "./migrations");
var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("database").WithFlywayMigration(flyway);
flyway.WaitFor(database);
```

## Additional Information

https://aspire.dev/integrations/databases/postgres/postgresql-extensions/

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
