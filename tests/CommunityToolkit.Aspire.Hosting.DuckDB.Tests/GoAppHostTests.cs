using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.DuckDB.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.DuckDB.AppHost.Go",
            exampleName: "duckdb",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
