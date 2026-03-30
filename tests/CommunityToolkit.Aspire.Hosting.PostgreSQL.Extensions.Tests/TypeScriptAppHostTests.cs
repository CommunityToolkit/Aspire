using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions",
            exampleName: "postgres-ext",
            waitForResources: ["postgres", "postgres-dbgate", "postgres-adminer"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
