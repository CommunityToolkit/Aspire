using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.VLLM.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.VLLM.AppHost.Go",
            exampleName: "vllm",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
