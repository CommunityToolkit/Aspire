#pragma warning disable CTASPIRE002
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Sqlite.Tests;

[RequiresWindows(Reason = "The NuGet package being used for the extension is Windows-only.")]
public class ResourceWithExtensionTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task ResourceCreatedWithExtensionIsAccessible()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var sqlite = builder.AddSqlite("sqlite")
            .WithNuGetExtension("mod_spatialite");

        await using var app = builder.Build();

        await app.StartAsync();

        var hb = Host.CreateApplicationBuilder();

        hb.AddSqliteConnection(sqlite.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        var connection = host.Services.GetRequiredService<SqliteConnection>();

        var result = await IsExtensionLoadedAsync(connection, "spatialite_version()");

        Assert.NotNull(result);
        Assert.IsType<string>(result);
        Assert.Equal("5.1.0", result);
    }

    private static async Task<object?> IsExtensionLoadedAsync(SqliteConnection connection, string checkFunction)
    {
        string checkQuery = $"SELECT {checkFunction}";
        using var command = connection.CreateCommand();
        command.CommandText = checkQuery;
        return await command.ExecuteScalarAsync();
    }
}