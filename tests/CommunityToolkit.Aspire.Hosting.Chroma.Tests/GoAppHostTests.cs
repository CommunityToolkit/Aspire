using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Chroma.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Chroma.AppHost.Go",
            exampleName: "chromadb",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
