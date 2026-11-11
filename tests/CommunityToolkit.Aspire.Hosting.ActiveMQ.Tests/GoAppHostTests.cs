using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.ActiveMQ.AppHost.Go",
            exampleName: "activemq",
            validatePublish: false,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
