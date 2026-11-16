using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flyway.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Flyway.AppHost.Go",
            exampleName: "flyway",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
