namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Runs the shared TypeScript AppHost validation flow for example app hosts.
/// </summary>
public static class TypeScriptAppHostTest
{
    /// <summary>
    /// Runs the TypeScript AppHost validation script for an example app host.
    /// </summary>
    /// <param name="appHostProject">The app host project directory name under the example folder.</param>
    /// <param name="packageName">The integration package name and project directory name.</param>
    /// <param name="exampleName">The example folder name.</param>
    /// <param name="waitForResources">The resources that must reach the expected Aspire state.</param>
    /// <param name="requiredCommands">Optional commands that must exist on <c>PATH</c> before validation runs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task Run(
        string appHostProject,
        string packageName,
        string exampleName,
        IEnumerable<string> waitForResources,
        IEnumerable<string>? requiredCommands = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(exampleName);
        ArgumentNullException.ThrowIfNull(waitForResources);

        List<string> resources = waitForResources
            .Where(static resource => !string.IsNullOrWhiteSpace(resource))
            .ToList();

        if (resources.Count == 0)
        {
            throw new ArgumentException("At least one resource name is required.", nameof(waitForResources));
        }

        List<string> commands = requiredCommands?
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .ToList() ?? [];

        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repoRoot, "eng", "testing", "validate-typescript-apphost.ps1");
        string appHostPath = Path.Combine(repoRoot, "examples", exampleName, appHostProject, "apphost.ts");
        string packageProjectPath = Path.Combine(repoRoot, "src", packageName, $"{packageName}.csproj");
        string shell = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        List<string> arguments =
        [
            "-NoLogo",
            "-NoProfile",
            "-File", scriptPath,
            "-AppHostPath", appHostPath,
            "-PackageProjectPath", packageProjectPath,
            "-PackageName", packageName,
            "-WaitForResources", string.Join(',', resources)
        ];

        if (commands.Count > 0)
        {
            arguments.Add("-RequiredCommands");
            arguments.Add(string.Join(',', commands));
        }

        await ProcessTestUtilities.RunProcessAsync(shell, arguments, repoRoot, cancellationToken);
    }
}
