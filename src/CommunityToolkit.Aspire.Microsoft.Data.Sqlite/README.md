# CommunityToolkit.Aspire.Microsoft.Data.Sqlite library

Register a `SqliteConnection` in the DI container to interact with a SQLite database using ADO.NET.

## Getting Started

### Prerequisites

-   A SQLite database

### Install the package

Install the .NET Aspire EF Core Sqlite library using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Microsoft.Data.Sqlite
```

### Example usage

In the _Program.cs_ file of your project, call the `AddSqliteConnection` extension method to register the `SqliteConnection` implementation in the DI container. This method takes the connection name as a parameter:

```csharp
builder.AddSqliteConnection("sqlite");
```

Then, in your service, inject `SqliteConnection` and use it to interact with the database:

```csharp
public class MyService(SqliteConnection context)
{
    // ...
}
```

## Additional documentation

-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/sqlite
-   https://learn.microsoft.com/dotnet/standard/data/sqlite

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
