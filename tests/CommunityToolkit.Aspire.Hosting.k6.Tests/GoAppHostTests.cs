using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.k6.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.k6.AppHost.Go",
            exampleName: "k6",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
