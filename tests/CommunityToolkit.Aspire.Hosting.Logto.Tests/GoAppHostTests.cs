using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Logto.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Logto.AppHost.Go",
            exampleName: "logto",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
