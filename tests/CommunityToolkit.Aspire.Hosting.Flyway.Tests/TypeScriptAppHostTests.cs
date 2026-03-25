using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flyway.Tests;

[RequiresDocker]
public sealed class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndRunsFlywayTasks()
    {
        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repoRoot, "eng", "testing", "validate-typescript-apphost.ps1");
        string appHostPath = Path.Combine(repoRoot, "examples", "flyway", "CommunityToolkit.Aspire.Hosting.Flyway.AppHost.TypeScript", "apphost.ts");
        string packageProjectPath = Path.Combine(repoRoot, "src", "CommunityToolkit.Aspire.Hosting.Flyway", "CommunityToolkit.Aspire.Hosting.Flyway.csproj");
        string shell = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        await ProcessTestUtilities.RunProcessAsync(shell, [
            "-NoLogo",
            "-NoProfile",
            "-File", scriptPath,
            "-AppHostPath", appHostPath,
            "-PackageProjectPath", packageProjectPath,
            "-PackageName", "CommunityToolkit.Aspire.Hosting.Flyway",
            "-WaitForResources", "flyway,flyway-telemetry",
            "-WaitStatus", "down"
        ], repoRoot, TestContext.Current.CancellationToken);
    }
}
