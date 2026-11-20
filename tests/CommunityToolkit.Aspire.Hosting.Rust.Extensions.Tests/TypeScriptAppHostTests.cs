using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Rust.Extensions.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Rust.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Rust.Extensions",
            exampleName: "rust",
            waitForResources: ["rust-app"],
            requiredCommands: ["cargo", "rustc"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
