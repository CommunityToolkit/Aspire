using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dbx.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Dbx.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Dbx",
            exampleName: "dbx",
            waitForResources: ["dbx"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
