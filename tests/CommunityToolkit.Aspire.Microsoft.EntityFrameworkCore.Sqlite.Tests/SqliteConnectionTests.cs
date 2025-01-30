using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite.Tests;

public class SqliteConnectionTests
{
    [Fact]
    public void ReadsFromConnectionStringCorrectly()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite", "Data Source=:memory:")
        ]);

        builder.AddSqliteDbContext<TestDbContext>("sqlite");

        using var host = builder.Build();

        var client = host.Services.GetRequiredService<TestDbContext>();

        Assert.NotNull(client.Database.GetConnectionString());
        Assert.Equal("data source=:memory:", client.Database.GetConnectionString());
    }

    [Fact]
    public void CanSetConnectionStringInCode()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite", "Data Source=/tmp/not-used.db")
        ]);

        builder.AddSqliteDbContext<TestDbContext>("sqlite", settings => settings.ConnectionString = "Data Source=:memory:");
        using var host = builder.Build();
        var client = host.Services.GetRequiredService<TestDbContext>();

        Assert.NotNull(client.Database.GetConnectionString());
        Assert.Equal("data source=:memory:", client.Database.GetConnectionString());
    }

    [Fact]
    public void CanSetConnectionStringInCodeWithKey()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Aspire:Sqlite:sqlite:ConnectionString", "Data Source=/tmp/not-used.db"),
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite", "Data Source=:memory:")
        ]);

        builder.AddSqliteDbContext<TestDbContext>("sqlite");

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<TestDbContext>();

        Assert.NotNull(client.Database.GetConnectionString());
        Assert.Equal("data source=:memory:", client.Database.GetConnectionString());
    }
}