using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.PowerShell.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.PowerShell.AppHost.Go",
            exampleName: "powershell",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
