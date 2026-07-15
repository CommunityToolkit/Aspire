using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RustFs.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.RustFs.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.RustFs",
            exampleName: "rustfs",
            waitForResources: ["rustfs"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
