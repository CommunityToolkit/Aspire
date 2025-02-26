#pragma warning disable CTASPIRE002
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Sqlite.Tests;

public class ResourceWithExtensionTests(ITestOutputHelper testOutputHelper)
{
    [Fact(Skip = "Skipping until there is a viable NuGet package for sqlite-vec we can use.")]
    public async Task ResourceCreatedWithExtensionIsAccessible()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var sqlite = builder.AddSqlite("sqlite")
            .WithNuGetExtension("sqlite-vec");

        await using var app = builder.Build();

        await app.StartAsync();

        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{sqlite.Resource.Name}"] = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddSqliteConnection(sqlite.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        var connection = host.Services.GetRequiredService<SqliteConnection>();

        var result = await IsExtensionLoadedAsync(connection, "vec_version()");

        Assert.NotNull(result);
        var version = Assert.IsType<string>(result);
        Assert.Equal("0.1.16", version);
    }

    private static async Task<object?> IsExtensionLoadedAsync(SqliteConnection connection, string checkFunction)
    {
        await connection.OpenAsync();
        string checkQuery = $"SELECT {checkFunction}";
        using var command = connection.CreateCommand();
        command.CommandText = checkQuery;
        return await command.ExecuteScalarAsync();
    }
}