using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flyway.Tests;

[RequiresDocker]
public sealed class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndRunsFlywayTasks()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Flyway.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Flyway",
            exampleName: "flyway",
            waitForResources: ["flyway", "flyway-telemetry"],
            waitStatus: "down",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
