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
    /// <param name="waitForResources">The resources that must reach the expected Aspire state, if any.</param>
    /// <param name="waitStatus">The Aspire resource status to wait for.</param>
    /// <param name="requiredCommands">Optional commands that must exist on <c>PATH</c> before validation runs.</param>
    /// <param name="useConfiguredPackages"><see langword="true"/> to validate the AppHost using the package mappings already present in <c>aspire.config.json</c> instead of packing a local polyglot package first.</param>
    /// <param name="httpProbeResource">Optional resource display name to probe over HTTP after startup validation completes.</param>
    /// <param name="httpProbePath">Optional relative path to request from the probed HTTP endpoint.</param>
    /// <param name="httpProbeExpectedText">Optional exact response text expected from the HTTP probe.</param>
    /// <param name="httpProbeEndpointName">The named endpoint to probe when <paramref name="httpProbeResource"/> is provided.</param>
    /// <param name="secrets">Optional dictionary of secret key-value pairs to set via <c>aspire secret set</c> before starting the app host.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task Run(
        string appHostProject,
        string packageName,
        string exampleName,
        IEnumerable<string> waitForResources,
        string waitStatus = "healthy",
        IEnumerable<string>? requiredCommands = null,
        bool useConfiguredPackages = false,
        string? httpProbeResource = null,
        string? httpProbePath = null,
        string? httpProbeExpectedText = null,
        string httpProbeEndpointName = "http",
        Dictionary<string, string>? secrets = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appHostProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(exampleName);
        ArgumentNullException.ThrowIfNull(waitForResources);

        if (waitStatus is not "healthy" and not "up" and not "down")
        {
            throw new ArgumentException("Wait status must be one of 'healthy', 'up', or 'down'.", nameof(waitStatus));
        }

        bool hasHttpProbeConfiguration =
            !string.IsNullOrWhiteSpace(httpProbeResource)
            || !string.IsNullOrWhiteSpace(httpProbePath)
            || httpProbeExpectedText is not null;

        if (hasHttpProbeConfiguration &&
            (string.IsNullOrWhiteSpace(httpProbeResource)
            || string.IsNullOrWhiteSpace(httpProbePath)
            || httpProbeExpectedText is null))
        {
            throw new ArgumentException(
                "HTTP probing requires a resource name, request path, and expected response text.");
        }

        if (hasHttpProbeConfiguration && string.IsNullOrWhiteSpace(httpProbeEndpointName))
        {
            throw new ArgumentException("HTTP probe endpoint name cannot be empty.", nameof(httpProbeEndpointName));
        }

        List<string> resources = waitForResources
            .Where(static resource => !string.IsNullOrWhiteSpace(resource))
            .ToList();

        List<string> commands = requiredCommands?
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .ToList() ?? [];

        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string scriptPath = Path.Combine(repoRoot, "eng", "testing", "validate-typescript-apphost.ps1");
        string appHostPath = Path.Combine(repoRoot, "examples", exampleName, appHostProject, "apphost.mts");
        string shell = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

        List<string> arguments =
        [
            "-NoLogo",
            "-NoProfile",
            "-File", scriptPath,
            "-AppHostPath", appHostPath
        ];

        if (resources.Count > 0)
        {
            arguments.Add("-WaitForResources");
            arguments.Add(string.Join(',', resources));

            if (!string.Equals(waitStatus, "healthy", StringComparison.Ordinal))
            {
                arguments.Add("-WaitStatus");
                arguments.Add(waitStatus);
            }
        }

        if (commands.Count > 0)
        {
            arguments.Add("-RequiredCommands");
            arguments.Add(string.Join(',', commands));
        }

        if (useConfiguredPackages)
        {
            arguments.Add("-UseConfiguredPackages");
        }

        if (hasHttpProbeConfiguration)
        {
            arguments.Add("-HttpProbeResource");
            arguments.Add(httpProbeResource!);
            arguments.Add("-HttpProbePath");
            arguments.Add(httpProbePath!);
            arguments.Add("-HttpProbeExpectedText");
            arguments.Add(httpProbeExpectedText!);
            arguments.Add("-HttpProbeEndpointName");
            arguments.Add(httpProbeEndpointName);
        }

        if (secrets is { Count: > 0 })
        {
            arguments.Add("-Secrets");
            arguments.Add(string.Join(',', secrets.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }

        await ProcessTestUtilities.RunProcessAsync(shell, arguments, repoRoot, cancellationToken);
    }
}