using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.PapercutSmtp.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.PapercutSmtp.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.PapercutSmtp",
            exampleName: "papercut",
            waitForResources: ["papercut", "papercut-default"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
