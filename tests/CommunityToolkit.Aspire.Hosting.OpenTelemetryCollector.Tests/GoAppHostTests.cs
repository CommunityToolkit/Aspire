using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.AppHost.Go",
            exampleName: "opentelemetry-collector",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
