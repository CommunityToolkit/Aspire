using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Zitadel.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Zitadel.AppHost.Go",
            exampleName: "zitadel",
            validatePublish: false,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
