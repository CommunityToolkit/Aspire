using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Ollama.AppHost.Go",
            exampleName: "ollama",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
