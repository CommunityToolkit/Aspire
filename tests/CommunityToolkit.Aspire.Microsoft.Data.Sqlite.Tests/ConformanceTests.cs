using Aspire.Components.ConformanceTests;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Microsoft.Data.Sqlite.Tests;

public class ConformanceTests : ConformanceTests<SqliteConnection, SqliteConnectionSettings>
{
    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Scoped;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.db");

        configuration.AddInMemoryCollection(
            [
                new(CreateConfigKey("Aspire:Sqlite:Client", key, "ConnectionString"), $"Data Source={dbPath}"),
                new("ConnectionStrings:sqlite", $"Data Source={dbPath}")
            ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<SqliteConnectionSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddSqliteConnection("sqlite", configure);
        }
        else
        {
            builder.AddKeyedSqliteConnection(key, configure);
        }
    }

    protected override void SetHealthCheck(SqliteConnectionSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetMetrics(SqliteConnectionSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(SqliteConnectionSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(SqliteConnection service)
    {
        service.Open();
    }

    protected override string ValidJsonConfig =>
        """
        {
        "Aspire": {
            "Sqlite": {
                "ConnectionString": "Data Source=/tmp/aspire.db"
            }
        }
        }
        """;
}
