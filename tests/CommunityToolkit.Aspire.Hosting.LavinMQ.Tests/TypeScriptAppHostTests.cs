using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.LavinMQ.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repoRoot, "eng", "testing", "validate-typescript-apphost.ps1");
        string appHostPath = Path.Combine(repoRoot, "examples", "lavinmq", "CommunityToolkit.Aspire.Hosting.LavinMQ.AppHost.TypeScript", "apphost.ts");
        string packageProjectPath = Path.Combine(repoRoot, "src", "CommunityToolkit.Aspire.Hosting.LavinMQ", "CommunityToolkit.Aspire.Hosting.LavinMQ.csproj");
        string shell = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        await ProcessTestUtilities.RunProcessAsync(shell, [
            "-NoLogo",
            "-NoProfile",
            "-File", scriptPath,
            "-AppHostPath", appHostPath,
            "-PackageProjectPath", packageProjectPath,
            "-PackageName", "CommunityToolkit.Aspire.Hosting.LavinMQ",
            "-WaitForResources", "volume-broker,bind-broker"
        ], repoRoot, TestContext.Current.CancellationToken);
    }
}
