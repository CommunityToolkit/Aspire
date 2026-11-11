using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Mosquitto.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Mosquitto.AppHost.Go",
            exampleName: "mosquitto",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
