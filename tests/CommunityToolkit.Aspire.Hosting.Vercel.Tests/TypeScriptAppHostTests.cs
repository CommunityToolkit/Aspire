using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Vercel.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Vercel.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Vercel",
            exampleName: "vercel",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}

