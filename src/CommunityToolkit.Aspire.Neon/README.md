# CommunityToolkit.Aspire.Neon library

This integration provides client support for [Neon](https://neon.tech/), a serverless PostgreSQL-compatible database, in .NET Aspire applications.

## Getting Started

### Install the package

In your client project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Neon
```

### Example usage

In the _Program.cs_ file of your client project, add the Neon data source:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Neon data source
builder.AddNeonDataSource("neondb");

var app = builder.Build();
```

You can then retrieve the `NpgsqlDataSource` instance using dependency injection:

```csharp
public class MyService
{
    private readonly NpgsqlDataSource _dataSource;

    public MyService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> GetCountAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM users";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
```

## Configuration

The Neon client integration supports the following configuration options:

```json
{
  "Aspire": {
    "Neon": {
      "neondb": {
        "ConnectionString": "Host=myhost;Database=mydb;Username=myuser;Password=mypass",
        "DisableHealthChecks": false,
        "DisableTracing": false,
        "DisableMetrics": false
      }
    }
  }
}
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/neon

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
