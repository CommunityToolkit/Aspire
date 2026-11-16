using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CpanmApiIntegration.AppHost.Go",
            exampleName: "perl",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
