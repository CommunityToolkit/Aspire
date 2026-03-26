using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Zitadel.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Zitadel.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Zitadel",
            exampleName: "zitadel",
            waitForResources: ["postgres", "zitadel", "zitadel-minimal"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}