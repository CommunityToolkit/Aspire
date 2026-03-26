namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class DacpacDeployerTests
{
    [Theory]
    [InlineData("Server=localhost,15433;Database=TargetDatabase;User ID=sa;Password=Password123!;Encrypt=False;", "TargetDatabase")]
    [InlineData("Server=localhost,15433;Initial Catalog=TargetDatabase;User ID=sa;Password=Password123!;Encrypt=False;", "TargetDatabase")]
    [InlineData("Server=localhost,15433;InitialCatalog=TargetDatabase;User ID=sa;Password=Password123!;Encrypt=False;", "TargetDatabase")]
    public void GetDatabaseName_ReturnsConfiguredDatabaseName(string connectionString, string expectedDatabaseName)
    {
        var databaseName = DacpacDeployer.GetDatabaseName(connectionString);

        Assert.Equal(expectedDatabaseName, databaseName);
    }

    [Fact]
    public void GetDatabaseName_WithoutTargetDatabase_Throws()
    {
        var connectionString = "Server=localhost,15433;User ID=sa;Password=Password123!;Encrypt=False;";

        var exception = Assert.Throws<InvalidOperationException>(() => DacpacDeployer.GetDatabaseName(connectionString));

        Assert.Contains("Database", exception.Message, StringComparison.Ordinal);
    }
}
