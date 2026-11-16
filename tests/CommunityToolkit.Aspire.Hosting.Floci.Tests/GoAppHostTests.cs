using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Floci.AppHost.Go",
            exampleName: "floci",
            // The Service Bus emulator is run-only and rejects publish-mode model construction.
            validatePublish: false,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
