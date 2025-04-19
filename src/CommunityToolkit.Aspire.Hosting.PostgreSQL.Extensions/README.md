# CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions library

This integration contains extensions for the [PostgreSQL hosting package](https://nuget.org/packages/Aspire.Hosting.PostgreSQL) for .NET Aspire.

The integration provides support for running [DbGate](https://github.com/dbgate/dbgate) and [Adminer](https://github.com/vrana/adminer) to interact with the PostgreSQL database.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an Postgres resource, then call `AddPostgres`:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithDbGate()
    .WithAdminer();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-postgres-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire