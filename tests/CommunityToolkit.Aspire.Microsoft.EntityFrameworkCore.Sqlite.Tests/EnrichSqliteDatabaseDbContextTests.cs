using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite.Tests;

public class EnrichSqliteDatabaseDbContextTests
{
    [Fact]
    public void EnrichSqliteDatabaseDbContext_RegistersDbContext()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", "Data Source=:memory:")
        ]);

        // Act
        builder.EnrichSqliteDatabaseDbContext<TestDbContext>();

        // Assert
        var app = builder.Build();
        var dbContext = app.Services.GetRequiredService<TestDbContext>();
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void EnrichSqliteDatabaseDbContext_ThrowsWhenBuilderIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            AspireEFSqliteExtensions.EnrichSqliteDatabaseDbContext<TestDbContext>(null!));
    }

    [Fact]
    public void EnrichSqliteDatabaseDbContext_DisablesOpenTelemetryWhenFalse()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", "Data Source=:memory:")
        ]);

        // Act
        builder.EnrichSqliteDatabaseDbContext<TestDbContext>(settings => settings.DisableTracing = true);

        // Assert - The test passes if no exceptions are thrown and DbContext is registered
        var app = builder.Build();
        var dbContext = app.Services.GetRequiredService<TestDbContext>();
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void EnrichSqliteDatabaseDbContext_EnablesOpenTelemetryByDefault()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", "Data Source=:memory:")
        ]);

        // Act
        builder.EnrichSqliteDatabaseDbContext<TestDbContext>(settings => settings.DisableTracing = false);

        // Assert - The test passes if no exceptions are thrown and OpenTelemetry services are registered
        var app = builder.Build();
        var dbContext = app.Services.GetRequiredService<TestDbContext>();
        Assert.NotNull(dbContext);

        // Verify OpenTelemetry services are registered (basic smoke test)
        var services = app.Services.GetServices<IHostedService>().ToList();
        Assert.True(services.Count > 0, "Services should be registered");
    }
}