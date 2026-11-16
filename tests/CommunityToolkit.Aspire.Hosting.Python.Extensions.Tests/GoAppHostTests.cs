using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Python.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Python.Extensions.AppHost.Go",
            exampleName: "python",
            validatePublish: false,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
