using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Sftp.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Sftp.AppHost.Go",
            exampleName: "sftp",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
