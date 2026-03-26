using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Python.Extensions.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Python.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Python.Extensions",
            exampleName: "python",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}