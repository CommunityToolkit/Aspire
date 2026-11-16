using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RedPanda.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.RedPanda.AppHost.Go",
            exampleName: "redpanda",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
