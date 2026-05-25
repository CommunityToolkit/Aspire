# CommunityToolkit.Aspire.Hosting.DuckDB library

Provides extension methods and resource definitions for the Aspire AppHost to support creating and running DuckDB databases.

DuckDB is an in-process analytical (OLAP) database, ideal for analytics workloads, Parquet/CSV querying, and data processing.

By default, the DuckDB resource will create a new DuckDB database in a temporary location. You can also specify a path to an existing DuckDB database file.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.DuckDB
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a DuckDB resource, then call `AddDuckDB`:

```csharp
var duckdb = builder.AddDuckDB("analytics");
```

To use a read-only database for analytics:

```csharp
var duckdb = builder.AddDuckDB("warehouse")
    .WithReadOnly();
```

## Additional Information

https://duckdb.org

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
