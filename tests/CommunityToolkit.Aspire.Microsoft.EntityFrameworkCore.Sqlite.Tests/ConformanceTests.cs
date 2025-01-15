using Aspire.Components.ConformanceTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite.Tests;

public class ConformanceTests : ConformanceTests<TestDbContext, SqliteEntityFrameworkCoreSettings>
{
    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.db");

        configuration.AddInMemoryCollection(
            [
                new(CreateConfigKey("Aspire:Sqlite:EntityFrameworkCore:Sqlite", key, "ConnectionString"), $"Data Source={dbPath}"),
                new($"ConnectionStrings:{key}", $"Data Source={dbPath}")
            ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<SqliteEntityFrameworkCoreSettings>? configure = null, string? key = null) =>
        builder.AddSqliteDbContext<TestDbContext>(key ?? "sqlite", configure);

    protected override void SetHealthCheck(SqliteEntityFrameworkCoreSettings options, bool enabled) =>
        options.DisableHealthChecks = !enabled;


    protected override void SetMetrics(SqliteEntityFrameworkCoreSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(SqliteEntityFrameworkCoreSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(TestDbContext service)
    {
        if (service.Database.CanConnect())
        {
            service.Database.EnsureCreated();
        }
    }

    protected override string ValidJsonConfig =>
        """
        {
        "Aspire": {
            "Sqlite": {
                "EntityFrameworkCore": {
                    "Sqlite": {
                        "ConnectionString": "Data Source=/tmp/aspire.db"
                    }
                }
            }
        }
        }
        """;
}
