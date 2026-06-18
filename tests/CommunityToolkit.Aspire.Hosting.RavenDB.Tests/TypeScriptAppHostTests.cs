using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RavenDB.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.RavenDB.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.RavenDB",
            exampleName: "ravendb",
            waitForResources: ["ravendb", "apiservice"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
