using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Chroma.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Chroma.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Chroma",
            exampleName: "chromadb",
            waitForResources: ["chroma"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
