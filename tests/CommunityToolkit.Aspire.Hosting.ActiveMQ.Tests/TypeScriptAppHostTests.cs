using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repoRoot, "eng", "testing", "validate-typescript-apphost.ps1");
        string appHostPath = Path.Combine(repoRoot, "examples", "activemq", "CommunityToolkit.Aspire.Hosting.ActiveMQ.AppHost.TypeScript", "apphost.ts");
        string packageProjectPath = Path.Combine(repoRoot, "src", "CommunityToolkit.Aspire.Hosting.ActiveMQ", "CommunityToolkit.Aspire.Hosting.ActiveMQ.csproj");
        string shell = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        await ProcessTestUtilities.RunProcessAsync(shell, [
            "-NoLogo",
            "-NoProfile",
            "-File", scriptPath,
            "-AppHostPath", appHostPath,
            "-PackageProjectPath", packageProjectPath,
            "-PackageName", "CommunityToolkit.Aspire.Hosting.ActiveMQ",
            "-WaitForResources", "classic,classic2,artemis,artemis2"
        ], repoRoot, TestContext.Current.CancellationToken);
    }
}
