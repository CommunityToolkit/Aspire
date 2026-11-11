using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MySql.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.MySql.Extensions.AppHost.Go",
            exampleName: "mysql-ext",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
