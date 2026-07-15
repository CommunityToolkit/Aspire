using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.K3s.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.K3s",
            exampleName: "k3s",
            waitForResources: ["k8s", "podinfo-web"],
            waitTimeoutSeconds: 300,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
