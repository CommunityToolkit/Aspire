using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Ollama.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Ollama",
            exampleName: "ollama",
            waitForResources: ["ollama", "ollama2"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
