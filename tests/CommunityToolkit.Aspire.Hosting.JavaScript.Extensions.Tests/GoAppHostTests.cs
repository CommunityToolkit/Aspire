using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.AppHost.Go",
            exampleName: "javascript-ext",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
