using CommunityToolkit.Aspire.Testing;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Vercel.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Vercel.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Vercel",
            exampleName: "vercel",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TypeScriptAppHostPublishesWithExplicitComputeEnvironment()
    {
        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string appHostRoot = Path.Combine(repoRoot, "examples", "vercel", "CommunityToolkit.Aspire.Hosting.Vercel.AppHost.TypeScript");
        string outputRoot = Path.Combine(appHostRoot, "aspire-output");
        string distRoot = Path.Combine(appHostRoot, "dist");

        try
        {
            await ProcessTestUtilities.RunProcessAsync("aspire", ["restore"], appHostRoot, TestContext.Current.CancellationToken);
            string npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
            await ProcessTestUtilities.RunProcessAsync(npm, ["run", "build"], appHostRoot, TestContext.Current.CancellationToken);
            await ProcessTestUtilities.RunProcessAsync("aspire", ["publish", "--non-interactive"], appHostRoot, TestContext.Current.CancellationToken);

            string planPath = Path.Combine(outputRoot, "vercel", "vercel-deployments.json");
            using var document = JsonDocument.Parse(File.ReadAllText(planPath));
            JsonElement deployment = Assert.Single(document.RootElement.GetProperty("deployments").EnumerateArray());

            Assert.Equal("api", deployment.GetProperty("resourceName").GetString());
            Assert.True(File.Exists(Path.Combine(outputRoot, "docker", "docker-compose.yaml")));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }

            if (Directory.Exists(distRoot))
            {
                Directory.Delete(distRoot, recursive: true);
            }
        }
    }
}
