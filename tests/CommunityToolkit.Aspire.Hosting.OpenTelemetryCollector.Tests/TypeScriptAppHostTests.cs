using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector",
            exampleName: "opentelemetry-collector",
            waitForResources: ["collector", "collector-routed"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
