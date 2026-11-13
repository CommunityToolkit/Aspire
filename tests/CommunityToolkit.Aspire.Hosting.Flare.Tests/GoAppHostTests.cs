using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flare.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Flare.AppHost.Go",
            exampleName: "flare",
            // The preview Go SDK cannot serialize the optional dashboard URL parameter handle.
            validatePublish: false,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
