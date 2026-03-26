using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Sftp.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Sftp.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Sftp",
            exampleName: "sftp",
            waitForResources: ["sftp", "sftp-defaults"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}