using DuckDB.NET.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.DuckDB.NET.Data.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ConnectionStringFromConfiguration()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Aspire:DuckDB:Client:ConnectionString", "DataSource=:memory:")
        ]);

        builder.AddDuckDBConnection("duckdb");

        using var host = builder.Build();
        var connection = host.Services.GetRequiredService<DuckDBConnection>();

        Assert.Equal("DataSource=:memory:", connection.ConnectionString);
    }

    [Fact]
    public void ThrowsWhenNoConnectionString()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddDuckDBConnection("duckdb");

        using var host = builder.Build();

        // Should throw when trying to resolve the connection because no connection string is configured
        Assert.Throws<InvalidOperationException>(() =>
            host.Services.GetRequiredService<DuckDBConnection>());
    }
}
