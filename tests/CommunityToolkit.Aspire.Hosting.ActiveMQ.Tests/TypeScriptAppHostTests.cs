using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.ActiveMQ.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.ActiveMQ",
            exampleName: "activemq",
            waitForResources: ["classic", "classic2", "artemis", "artemis2"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
