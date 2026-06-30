using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Floci.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Floci",
            exampleName: "floci",
            waitForResources: ["floci", "floci-custom", "floci-persistent", "floci-mount"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
