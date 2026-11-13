using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.PapercutSmtp.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.PapercutSmtp.AppHost.Go",
            exampleName: "papercut",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
