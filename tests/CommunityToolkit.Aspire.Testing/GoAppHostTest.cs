namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Runs the Go AppHost SDK generation, compilation, and model checks.
/// </summary>
public static class GoAppHostTest
{
    /// <summary>
    /// Restores, compiles, and builds the model for an example Go AppHost.
    /// </summary>
    /// <param name="appHostProject">The AppHost directory name under the example folder.</param>
    /// <param name="exampleName">The example folder name.</param>
    /// <param name="validatePublish">Whether to validate the publish-mode model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task Run(string appHostProject, string exampleName, bool validatePublish = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(exampleName);

        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string appHostDirectory = Path.Combine(repoRoot, "examples", exampleName, appHostProject);
        string aspire = OperatingSystem.IsWindows() ? "cmd.exe" : "aspire";
        string[] aspirePrefix = OperatingSystem.IsWindows() ? ["/d", "/c", "aspire"] : [];
        await ProcessTestUtilities.RunProcessAsync(aspire, [.. aspirePrefix, "restore", "--apphost", "apphost.go", "--non-interactive"], appHostDirectory, cancellationToken);
        await ProcessTestUtilities.RunProcessAsync("go", ["test", "./..."], appHostDirectory, cancellationToken);
        if (validatePublish)
        {
            await ProcessTestUtilities.RunProcessAsync(aspire, [.. aspirePrefix, "publish", "--apphost", "apphost.go", "--list-steps", "--non-interactive"], appHostDirectory, cancellationToken);
        }
    }
}
