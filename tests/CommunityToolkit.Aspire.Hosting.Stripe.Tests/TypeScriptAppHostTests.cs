using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Stripe.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    [RequiresAuthenticatedTool("stripe")]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Stripe.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Stripe",
            exampleName: "stripe",
            waitForResources: ["webhook-target", "stripe", "stripe-external", "consumer"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
