using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.SurrealDb.AppHost.Go",
            exampleName: "surrealdb",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
