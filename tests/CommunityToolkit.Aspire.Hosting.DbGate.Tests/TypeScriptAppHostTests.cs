using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.DbGate.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.DbGate.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.DbGate",
            exampleName: "dbgate",
            waitForResources: ["dbgate"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
