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
            waitForResources: ["floci-aws", "floci-az", "floci-gcp", "floci-custom", "floci-gcp-custom", "floci-persistent", "floci-mount"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
