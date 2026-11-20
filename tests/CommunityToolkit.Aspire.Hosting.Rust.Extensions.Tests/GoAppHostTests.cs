using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Rust.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Rust.Extensions.AppHost.Go",
            exampleName: "rust",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
