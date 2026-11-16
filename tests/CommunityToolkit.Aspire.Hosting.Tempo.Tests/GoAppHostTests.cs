using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Tempo.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Tempo.AppHost.Go",
            exampleName: "tempo",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
