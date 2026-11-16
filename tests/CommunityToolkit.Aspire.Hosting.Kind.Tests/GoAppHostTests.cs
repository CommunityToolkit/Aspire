using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task CrdWaitApisWorkFromGeneratedGoSdk()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Kind.AppHost.Go",
            exampleName: "kind",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
