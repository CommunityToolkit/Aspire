using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Java.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Java.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Java",
            exampleName: "java",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}