using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MailPit.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repoRoot, "eng", "testing", "validate-typescript-apphost.ps1");
        string appHostPath = Path.Combine(repoRoot, "examples", "mailpit", "CommunityToolkit.Aspire.Hosting.MailPit.AppHost.TypeScript", "apphost.ts");
        string packageProjectPath = Path.Combine(repoRoot, "src", "CommunityToolkit.Aspire.Hosting.MailPit", "CommunityToolkit.Aspire.Hosting.MailPit.csproj");
        string shell = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        await ProcessTestUtilities.RunProcessAsync(shell, [
            "-NoLogo",
            "-NoProfile",
            "-File", scriptPath,
            "-AppHostPath", appHostPath,
            "-PackageProjectPath", packageProjectPath,
            "-PackageName", "CommunityToolkit.Aspire.Hosting.MailPit",
            "-WaitForResources", "mailpit,mailpit-default"
        ], repoRoot, TestContext.Current.CancellationToken);
    }
}
