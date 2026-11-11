using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions.AppHost.Go",
            exampleName: "postgres-ext",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
