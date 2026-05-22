using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact(Skip = "Test failing due to problem with SqlClient in the TypeScript app host. See https://github.com/microsoft/aspire/issues/17011 for details.")]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder",
            exampleName: "data-api-builder",
            waitForResources: ["dab", "dab-with-options"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}