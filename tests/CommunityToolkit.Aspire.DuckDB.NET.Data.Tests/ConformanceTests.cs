using Aspire.Components.ConformanceTests;
using DuckDB.NET.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.DuckDB.NET.Data.Tests;

public class ConformanceTests : ConformanceTests<DuckDBConnection, DuckDBConnectionSettings>
{
    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Scoped;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        configuration.AddInMemoryCollection(
            [
                new(CreateConfigKey("Aspire:DuckDB:Client", key, "ConnectionString"), "DataSource=:memory:"),
                new("ConnectionStrings:duckdb", "DataSource=:memory:")
            ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<DuckDBConnectionSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddDuckDBConnection("duckdb", configure);
        }
        else
        {
            builder.AddKeyedDuckDBConnection(key, configure);
        }
    }

    protected override void SetHealthCheck(DuckDBConnectionSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetMetrics(DuckDBConnectionSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(DuckDBConnectionSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(DuckDBConnection service)
    {
        service.Open();
    }

    protected override string ValidJsonConfig =>
        """
        {
        "Aspire": {
            "DuckDB": {
                "ConnectionString": "DataSource=:memory:"
            }
        }
        }
        """;
}
