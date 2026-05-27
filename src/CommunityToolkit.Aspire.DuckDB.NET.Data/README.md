# CommunityToolkit.Aspire.DuckDB.NET.Data library

Register a `DuckDBConnection` in the DI container to interact with a DuckDB database using ADO.NET.

DuckDB is an in-process analytical (OLAP) database, ideal for analytics workloads, Parquet/CSV querying, and data processing.

## Getting Started

### Prerequisites

- A DuckDB database

### Install the package

Install the Aspire DuckDB client library using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.DuckDB.NET.Data
```

### Example usage

In the _Program.cs_ file of your project, call the `AddDuckDBConnection` extension method to register the `DuckDBConnection` implementation in the DI container. This method takes the connection name as a parameter:

```csharp
builder.AddDuckDBConnection("analytics");
```

Then, in your service, inject `DuckDBConnection` and use it to interact with the database:

```csharp
public class AnalyticsService(DuckDBConnection db)
{
    // ...
}
```

## Additional documentation

- https://duckdb.org
- https://github.com/Giorgi/DuckDB.NET

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
