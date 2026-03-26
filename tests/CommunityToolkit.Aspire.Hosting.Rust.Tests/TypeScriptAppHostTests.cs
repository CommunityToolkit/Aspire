using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Rust.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Rust",
            exampleName: "rust",
            waitForResources: ["rust-app"],
            requiredCommands: ["cargo", "rustc"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
