using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Stripe.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Stripe.AppHost.Go",
            exampleName: "stripe",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
