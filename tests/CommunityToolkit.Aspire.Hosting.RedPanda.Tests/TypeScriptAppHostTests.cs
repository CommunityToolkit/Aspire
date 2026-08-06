using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RedPanda.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.RedPanda.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.RedPanda",
            exampleName: "redpanda",
            waitForResources: ["redpanda", "redpanda-pinned"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
