using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using Microsoft.Data.SqlClient;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlDatabaseProjects_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlDatabaseProjects_AppHost>>
{
    [Theory]
    [InlineData("sdk-project", "SdkProject", "Database1")]
    [InlineData("other-sdk-project", "SdkProject", "Database3")]
    [InlineData("chinook", "InvoiceLine", "Database2")]
    public async Task ProjectBasedResourceStartsAndRespondsOk(string resourceName, string tableName, string database)
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName, KnownResourceStates.TerminalStates).WaitAsync(TimeSpan.FromMinutes(5));
        fixture.ResourceNotificationService.TryGetCurrentState(resourceName, out var resourceEvent);

        Assert.NotNull(resourceEvent);
        Assert.Equal(KnownResourceStates.Finished, resourceEvent.Snapshot.State?.Text);
        Assert.Equal(0, resourceEvent.Snapshot.ExitCode);

        string? connectionString = await fixture.GetConnectionString(database);
        Assert.NotNull(connectionString);

        // The AppHost runs in-process, so its SQL health checks share our SqlClient connection pool.
        // A health check that races DacFx's ALTER DATABASE can put that pool into its blocking period,
        // causing our OpenAsync to rethrow the cached "Login failed" error. Use a separate, non-blocking pool.
        // TODO: Remove once Aspire's SQL Server health checks use NeverBlock: https://github.com/microsoft/aspire/pull/20534
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock,
        };

        using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) " +
            "FROM   INFORMATION_SCHEMA.TABLES " +
            "WHERE  TABLE_SCHEMA = 'dbo' " +
           $"AND    TABLE_NAME = '{tableName}'";
        
        var result = await command.ExecuteScalarAsync();
        Assert.Equal(1, result);
    }
}