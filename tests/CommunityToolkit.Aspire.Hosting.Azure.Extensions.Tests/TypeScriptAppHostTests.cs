using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Azure.Extensions.Tests;

[RequiresContainerRuntime]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Azure.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Azure.Extensions",
            exampleName: "azure-ext",
            waitForResources: ["blobs-explorer"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
