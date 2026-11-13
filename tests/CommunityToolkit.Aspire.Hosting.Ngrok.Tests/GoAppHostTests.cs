using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Ngrok.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Ngrok.AppHost.Go",
            exampleName: "ngrok",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
