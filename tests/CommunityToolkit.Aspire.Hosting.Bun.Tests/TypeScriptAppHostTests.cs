using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Bun.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repoRoot, "eng", "testing", "validate-typescript-apphost.ps1");
        string appHostPath = Path.Combine(repoRoot, "examples", "bun", "CommunityToolkit.Aspire.Hosting.Bun.AppHost.TypeScript", "apphost.ts");
        string packageProjectPath = Path.Combine(repoRoot, "src", "CommunityToolkit.Aspire.Hosting.Bun", "CommunityToolkit.Aspire.Hosting.Bun.csproj");
        string shell = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        await ProcessTestUtilities.RunProcessAsync(shell, [
            "-NoLogo",
            "-NoProfile",
            "-File", scriptPath,
            "-AppHostPath", appHostPath,
            "-PackageProjectPath", packageProjectPath,
            "-PackageName", "CommunityToolkit.Aspire.Hosting.Bun",
            "-WaitForResources", "bun-app,bun-defaults,bun-watch",
            "-WaitStatus", "up",
            "-RequiredCommands", "bun"
        ], repoRoot, TestContext.Current.CancellationToken);
    }
}
