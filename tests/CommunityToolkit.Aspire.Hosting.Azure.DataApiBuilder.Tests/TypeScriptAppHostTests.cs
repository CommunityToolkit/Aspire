using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder",
            exampleName: "data-api-builder",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}