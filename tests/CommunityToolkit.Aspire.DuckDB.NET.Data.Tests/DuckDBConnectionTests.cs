using DuckDB.NET.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.DuckDB.NET.Data.Tests;

public class DuckDBConnectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsFromConnectionStringCorrectly(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:duckdb", "DataSource=:memory:")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedDuckDBConnection("duckdb");
        }
        else
        {
            builder.AddDuckDBConnection("duckdb");
        }

        using var host = builder.Build();

        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<DuckDBConnection>("duckdb") :
            host.Services.GetRequiredService<DuckDBConnection>();

        Assert.NotNull(client.ConnectionString);
        Assert.Contains("DataSource=:memory:", client.ConnectionString);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanSetConnectionStringInCode(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:duckdb", "DataSource=/tmp/not-used.duckdb")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedDuckDBConnection("duckdb", settings => settings.ConnectionString = "DataSource=:memory:");
        }
        else
        {
            builder.AddDuckDBConnection("duckdb", settings => settings.ConnectionString = "DataSource=:memory:");
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<DuckDBConnection>("duckdb") :
            host.Services.GetRequiredService<DuckDBConnection>();

        Assert.NotNull(client.ConnectionString);
        Assert.Contains("DataSource=:memory:", client.ConnectionString);
    }

    [Fact]
    public void CanSetMultipleKeyedConnections()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:duckdb1", "DataSource=/tmp/duckdb1.duckdb"),
            new KeyValuePair<string, string?>("ConnectionStrings:duckdb2", "DataSource=/tmp/duckdb2.duckdb")
        ]);

        builder.AddKeyedDuckDBConnection("duckdb1");
        builder.AddKeyedDuckDBConnection("duckdb2");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredKeyedService<DuckDBConnection>("duckdb1");
        var client2 = host.Services.GetRequiredKeyedService<DuckDBConnection>("duckdb2");

        Assert.NotNull(client1.ConnectionString);
        Assert.Contains("duckdb1", client1.ConnectionString);

        Assert.NotNull(client2.ConnectionString);
        Assert.Contains("duckdb2", client2.ConnectionString);
    }
}
