using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using Microsoft.Data.SqlClient;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlDatabaseProjects_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlDatabaseProjects_AppHost>>
{
    [Theory]
    [InlineData("sdk-project", "SdkProject")]
    [InlineData("chinook", "InvoiceLine")]
    public async Task ProjectBasedResourceStartsAndRespondsOk(string resourceName, string tableName)
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName, KnownResourceStates.Finished).WaitAsync(TimeSpan.FromMinutes(5));

        string? connectionString = await fixture.GetConnectionString("TargetDatabase");
        Assert.NotNull(connectionString);

        using var connection = new SqlConnection(connectionString);
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