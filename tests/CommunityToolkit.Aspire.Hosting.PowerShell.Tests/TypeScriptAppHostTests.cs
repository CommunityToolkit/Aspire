using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.PowerShell.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.PowerShell.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.PowerShell",
            exampleName: "powershell",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
