using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Redis.Extensions.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        string? originalRuntimeSmoke = Environment.GetEnvironmentVariable("ASPIRE_RUNTIME_SMOKE");
        Environment.SetEnvironmentVariable("ASPIRE_RUNTIME_SMOKE", "1");

        try
        {
            await TypeScriptAppHostTest.Run(
                appHostProject: "CommunityToolkit.Aspire.Hosting.Redis.Extensions.AppHost.TypeScript",
                packageName: "CommunityToolkit.Aspire.Hosting.Redis.Extensions",
                exampleName: "redis-ext",
                waitForResources: ["cache", "cache-dbgate"],
                waitStatus: "up",
                cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPIRE_RUNTIME_SMOKE", originalRuntimeSmoke);
        }
    }
}
