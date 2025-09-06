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

#### Option 1: Using IHostApplicationBuilder (Traditional Aspire Pattern)

In the _Program.cs_ file of your project, call the `AddSqliteDbContext<TDbContext>` extension method to register the `TDbContext` implementation in the DI container. This method takes the connection name as a parameter:

```csharp
builder.AddSqliteDbContext<BloggingContext>("sqlite");
```

#### Option 2: Using WebApplicationBuilder (New Simplified Pattern)

For ASP.NET Core applications, you can use the simplified `EnrichSqliteDatabaseDbContext<TDbContext>` extension method:

```csharp
// Basic usage with default connection string name "DefaultConnection"
builder.EnrichSqliteDatabaseDbContext<BloggingContext>();

// With custom connection string name
builder.EnrichSqliteDatabaseDbContext<BloggingContext>("MyConnection");

// Disable OpenTelemetry instrumentation
builder.EnrichSqliteDatabaseDbContext<BloggingContext>(enableOpenTelemetry: false);
```

The `EnrichSqliteDatabaseDbContext` method provides:
- **Simplified API**: Works directly with `WebApplicationBuilder`
- **Default connection string**: Uses "DefaultConnection" by default
- **OpenTelemetry integration**: Automatically adds EF Core instrumentation for distributed tracing
- **Parameter validation**: Proper error handling for missing connection strings

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
