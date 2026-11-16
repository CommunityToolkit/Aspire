using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MongoDB.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.MongoDB.Extensions.AppHost.Go",
            exampleName: "mongodb-ext",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
