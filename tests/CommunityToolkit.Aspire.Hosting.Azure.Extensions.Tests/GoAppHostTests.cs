using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Azure.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Azure.Extensions.AppHost.Go",
            exampleName: "azure-ext",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
