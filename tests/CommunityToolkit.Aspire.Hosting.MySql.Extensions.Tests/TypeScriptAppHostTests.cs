using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MySql.Extensions.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.MySql.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.MySql.Extensions",
            exampleName: "mysql-ext",
            waitForResources: ["mysql", "mysql-adminer", "mysql-dbgate"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}