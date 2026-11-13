using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.RavenDB.AppHost.Go",
            exampleName: "ravendb",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
