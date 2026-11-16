using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Adminer.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Adminer.AppHost.Go",
            exampleName: "adminer",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
