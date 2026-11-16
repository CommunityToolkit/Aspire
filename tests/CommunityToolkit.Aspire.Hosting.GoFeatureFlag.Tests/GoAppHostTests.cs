using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.GoFeatureFlag.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.GoFeatureFlag.AppHost.Go",
            exampleName: "goff",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
