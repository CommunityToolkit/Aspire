using CommunityToolkit.Aspire.Testing;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Vercel.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        if (!IsOnPath("pwsh"))
        {
            Assert.Skip("TypeScript AppHost start smoke requires PowerShell (pwsh) because the TypeScript AppHost test harness launches Aspire scripts through pwsh.");
        }

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
            string node = OperatingSystem.IsWindows() ? "node.exe" : "node";
            await ProcessTestUtilities.RunProcessAsync(node, [Path.Combine("node_modules", "eslint", "bin", "eslint.js"), "apphost.mts"], appHostRoot, TestContext.Current.CancellationToken);
            await ProcessTestUtilities.RunProcessAsync(node, [Path.Combine("node_modules", "typescript", "bin", "tsc")], appHostRoot, TestContext.Current.CancellationToken);
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

    private static bool IsOnPath(string fileName)
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return true;
            }

            if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe"))
            {
                return true;
            }
        }

        return false;
    }
}
