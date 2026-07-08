using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        // Squad resources are logical dashboard/resource-graph entries: they transition
        // Configured -> Spawning -> Active -> Finished and never run a server process, so
        // they never reach a "running"/"up"/"healthy" Aspire state. Waiting on them would
        // always time out. This test validates the meaningful path instead: the TypeScript
        // AppHost restores, compiles (tsc --noEmit), starts, and reports its resources
        // (verified by the script's `aspire describe` step).
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Squad.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Squad",
            exampleName: "squad",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
