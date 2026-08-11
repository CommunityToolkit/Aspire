using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Mosquitto.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Mosquitto.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Mosquitto",
            exampleName: "mosquitto",
            waitForResources: ["mqtt"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
