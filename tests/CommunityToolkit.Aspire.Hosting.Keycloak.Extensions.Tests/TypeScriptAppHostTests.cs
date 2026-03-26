using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Keycloak.Extensions.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Keycloak.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Keycloak.Extensions",
            exampleName: "keycloak-postgres",
            waitForResources: ["keycloak-postgres"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}