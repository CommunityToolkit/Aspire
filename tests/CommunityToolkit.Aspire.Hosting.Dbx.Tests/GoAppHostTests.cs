using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dbx.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Dbx.AppHost.Go",
            exampleName: "dbx",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
