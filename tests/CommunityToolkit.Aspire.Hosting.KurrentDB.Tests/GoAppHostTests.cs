using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.KurrentDB.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.KurrentDB.AppHost.Go",
            exampleName: "kurrentdb",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
