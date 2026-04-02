using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.SurrealDb.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.SurrealDb",
            exampleName: "surrealdb",
            waitForResources: ["primary", "mounted"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}