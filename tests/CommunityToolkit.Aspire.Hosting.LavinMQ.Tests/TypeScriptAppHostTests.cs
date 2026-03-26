using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.LavinMQ.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.LavinMQ.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.LavinMQ",
            exampleName: "lavinmq",
            waitForResources: ["volume-broker", "bind-broker"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
