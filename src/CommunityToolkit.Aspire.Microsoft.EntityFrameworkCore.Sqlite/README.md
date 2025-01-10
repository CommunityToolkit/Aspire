# CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite library

Register a `DbContext` in the DI container to interact with a SQLite database using Entity Framework Core.

## Getting Started

### Prerequisites

-   A `DbContext`
-   A SQLite database

### Install the package

Install the .NET Aspire EF Core Sqlite library using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite
```

### Example usage

In the _Program.cs_ file of your project, call the `AddSqliteDbContext<TDbContext>` extension method to register the `TDbContext` implementation in the DI container. This method takes the connection name as a parameter:

```csharp
builder.AddSqliteDbContext<BloggingContext>("sqlite");
```

Then, in your service, inject `TDbContext` and use it to interact with the database:

```csharp
public class MyService(BloggingContext context)
{
    // ...
}
```

## Additional documentation

-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/sqlite-entity-framework-integration
-   https://learn.microsoft.com/ef/core/providers/sqlite/

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
