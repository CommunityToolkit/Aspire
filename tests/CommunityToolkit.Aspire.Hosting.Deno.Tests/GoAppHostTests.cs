using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Deno.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Deno.AppHost.Go",
            exampleName: "deno",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
