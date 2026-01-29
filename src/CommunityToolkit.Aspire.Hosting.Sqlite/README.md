# CommunityToolkit.Aspire.Hosting.Sqlite library

Provides extension methods and resource definitions for the Aspire AppHost to support creating and running SQLite databases.

The integration also provides support for running [SQLite Web](https://github.com/coleifer/sqlite-web) to interact with the SQLite database.

By default, the Sqlite resource will create a new SQLite database in a temporary location. You can also specify a path to an existing SQLite database file.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Sqlite
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an Sqlite resource, then call `AddSqlite`:

```csharp
var sqlite = builder.AddSqlite("sqlite")
    .WithSqliteWeb();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/sqlite

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

