using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task CrdWaitApisWorkFromGeneratedTypeScriptSdk()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Kind.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Kind",
            exampleName: "kind",
            waitForResources: ["ts-kind", "ts-manifest", "ts-chart"],
            waitStatus: "up",
            requiredCommands: ["kind", "helm", "kubectl"],
            waitTimeoutSeconds: 300,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
