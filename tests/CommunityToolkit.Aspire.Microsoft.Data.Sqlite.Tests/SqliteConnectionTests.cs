using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Microsoft.Data.Sqlite.Tests;

public class SqliteConnectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsFromConnectionStringCorrectly(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite", "Data Source=:memory:")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedSqliteConnection("sqlite");
        }
        else
        {
            builder.AddSqliteConnection("sqlite");
        }

        using var host = builder.Build();

        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<SqliteConnection>("sqlite") :
            host.Services.GetRequiredService<SqliteConnection>();

        Assert.NotNull(client.ConnectionString);
        Assert.Equal("data source=:memory:", client.ConnectionString, ignoreCase: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanSetConnectionStringInCode(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite", "Data Source=/tmp/not-used.db")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedSqliteConnection("sqlite", settings => settings.ConnectionString = "Data Source=:memory:");
        }
        else
        {
            builder.AddSqliteConnection("sqlite", settings => settings.ConnectionString = "Data Source=:memory:");
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<SqliteConnection>("sqlite") :
            host.Services.GetRequiredService<SqliteConnection>();

        Assert.NotNull(client.ConnectionString);
        Assert.Equal("data source=:memory:", client.ConnectionString, ignoreCase: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanSetConnectionStringInCodeWithKey(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Aspire:Sqlite:sqlite:ConnectionString", "Data Source=/tmp/not-used.db"),
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite", "Data Source=:memory:")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedSqliteConnection("sqlite");
        }
        else
        {
            builder.AddSqliteConnection("sqlite");
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<SqliteConnection>("sqlite") :
            host.Services.GetRequiredService<SqliteConnection>();

        Assert.NotNull(client.ConnectionString);
        Assert.Equal("data source=:memory:", client.ConnectionString, ignoreCase: true);
    }

    [Fact]
    public void CanSetMultipleKeyedConnections()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite1", "Data Source=/tmp/sqlite1.db"),
            new KeyValuePair<string, string?>("ConnectionStrings:sqlite2", "Data Source=/tmp/sqlite2.db")
        ]);

        builder.AddKeyedSqliteConnection("sqlite1");
        builder.AddKeyedSqliteConnection("sqlite2");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredKeyedService<SqliteConnection>("sqlite1");
        var client2 = host.Services.GetRequiredKeyedService<SqliteConnection>("sqlite2");

        Assert.NotNull(client1.ConnectionString);
        Assert.Equal("data source=/tmp/sqlite1.db", client1.ConnectionString, ignoreCase: true);

        Assert.NotNull(client2.ConnectionString);
        Assert.Equal("data source=/tmp/sqlite2.db", client2.ConnectionString, ignoreCase: true);
    }
}
