using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MailPit.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.MailPit.AppHost.Go",
            exampleName: "mailpit",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
