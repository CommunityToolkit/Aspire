using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.AppHost.Go",
            exampleName: "data-api-builder",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
