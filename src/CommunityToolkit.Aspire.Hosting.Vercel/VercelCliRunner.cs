using Aspire.Hosting;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Runs typed Vercel and Docker CLI operations for the Vercel integration. Keeping this behind
/// an interface lets tests validate deploy behavior without invoking the real tools or passing
/// generic argument bags through deployment code.
/// </summary>
internal interface IVercelCliRunner
{
    Task<VercelCliResult> GetVersionAsync(CancellationToken cancellationToken);

    Task<VercelCliResult> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task<VercelCliResult> ListProjectsAsync(VercelEnvironmentOptionsAnnotation options, string? filter, CancellationToken cancellationToken);

    Task<VercelCliResult> AddProjectAsync(VercelEnvironmentOptionsAnnotation options, string projectName, CancellationToken cancellationToken);

    Task<VercelCliResult> RemoveProjectAsync(VercelEnvironmentOptionsAnnotation options, string projectName, CancellationToken cancellationToken);

    Task<VercelCliResult> LinkProjectAsync(VercelEnvironmentOptionsAnnotation options, string projectLinkDirectory, string projectNameOrId, CancellationToken cancellationToken);

    Task<VercelCliResult> PullProjectSettingsAsync(VercelEnvironmentOptionsAnnotation options, string projectLinkDirectory, string targetEnvironment, CancellationToken cancellationToken);

    Task<VercelCliResult> ListProjectEnvironmentVariablesAsync(VercelEnvironmentOptionsAnnotation options, string projectLinkDirectory, string targetEnvironment, CancellationToken cancellationToken);

    Task<VercelCliResult> AddProjectEnvironmentVariableAsync(VercelEnvironmentOptionsAnnotation options, string projectLinkDirectory, string name, string targetEnvironment, string value, CancellationToken cancellationToken);

    Task<VercelCliResult> RemoveProjectEnvironmentVariableAsync(VercelEnvironmentOptionsAnnotation options, string projectLinkDirectory, string name, string targetEnvironment, CancellationToken cancellationToken);

    Task<VercelCliResult> DeployPrebuiltAsync(VercelEnvironmentOptionsAnnotation options, string deployDirectory, string? projectNameOrId, IReadOnlyList<KeyValuePair<string, string>> environmentVariables, CancellationToken cancellationToken);

    Task<VercelCliResult> InspectDeploymentAsync(VercelEnvironmentOptionsAnnotation options, string deploymentUrl, CancellationToken cancellationToken);

    Task<VercelCliResult> ValidateDockerBuildxAsync(CancellationToken cancellationToken);

    Task<VercelCliResult> LoginToVcrAsync(string ownerId, string oidcToken, CancellationToken cancellationToken);

    Task<VercelCliResult> InspectDockerImageDigestAsync(string imageReference, CancellationToken cancellationToken);
}

/// <summary>
/// Process-based implementation of <see cref="IVercelCliRunner"/> that preserves argument and
/// stdin boundaries for cross-platform quoting and secret handling.
/// </summary>
internal sealed class VercelCliRunner : IVercelCliRunner
{
    internal delegate Task<VercelCliResult> ProcessRunner(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        string? standardInput);

    private readonly ProcessRunner _processRunner;

    public VercelCliRunner()
        : this(RunProcessAsync)
    {
    }

    internal VercelCliRunner(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public Task<VercelCliResult> GetVersionAsync(CancellationToken cancellationToken)
        => RunVercelAsync(["--version"], workingDirectory: null, cancellationToken);

    public Task<VercelCliResult> GetCurrentUserAsync(CancellationToken cancellationToken)
        => RunVercelAsync(["whoami"], workingDirectory: null, cancellationToken);

    public Task<VercelCliResult> ListProjectsAsync(
        VercelEnvironmentOptionsAnnotation options,
        string? filter,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["project", "ls"];
        AddOptionalScopeArgument(arguments, options);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            arguments.Add("--filter");
            arguments.Add(filter);
        }

        arguments.Add("--format=json");

        return RunVercelAsync(arguments, workingDirectory: null, cancellationToken);
    }

    public Task<VercelCliResult> AddProjectAsync(
        VercelEnvironmentOptionsAnnotation options,
        string projectName,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["project", "add", projectName];
        AddOptionalScopeArgument(arguments, options);

        return RunVercelAsync(arguments, workingDirectory: null, cancellationToken);
    }

    public Task<VercelCliResult> RemoveProjectAsync(
        VercelEnvironmentOptionsAnnotation options,
        string projectName,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["project", "remove", projectName];
        AddOptionalScopeArgument(arguments, options);

        return RunVercelAsync(arguments, workingDirectory: null, cancellationToken, standardInput: "y\n");
    }

    public Task<VercelCliResult> LinkProjectAsync(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string projectNameOrId,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["link"];
        AddOptionalScopeArgument(arguments, options);
        // Link writes .vercel/project.json in the selected working directory. Always link a
        // scratch directory; checked-in .vercel/project.json in the source root is read-only
        // user intent, not something deploy should mutate.
        // See https://vercel.com/docs/cli/link.
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--project");
        arguments.Add(projectNameOrId);

        return RunVercelAsync(arguments, projectLinkDirectory, cancellationToken);
    }

    public Task<VercelCliResult> PullProjectSettingsAsync(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string targetEnvironment,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["pull"];
        AddOptionalScopeArgument(arguments, options);
        // Pull materializes .vercel/project.json plus .vercel/.env.<environment>.local.
        // The integration reads the project identity and VCR OIDC token, then deletes the
        // pulled env files before writing deploy artifacts.
        // See https://vercel.com/docs/cli/pull.
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--environment");
        arguments.Add(targetEnvironment);

        return RunVercelAsync(arguments, projectLinkDirectory, cancellationToken);
    }

    public Task<VercelCliResult> ListProjectEnvironmentVariablesAsync(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string targetEnvironment,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["env", "ls", targetEnvironment];
        AddOptionalScopeArgument(arguments, options);
        // Environment variables are project-scoped in the Vercel CLI. The linked scratch
        // directory selects the project; JSON output avoids parsing localized table text.
        // See https://vercel.com/docs/cli/env.
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--format=json");

        return RunVercelAsync(arguments, projectLinkDirectory, cancellationToken);
    }

    public Task<VercelCliResult> AddProjectEnvironmentVariableAsync(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment,
        string value,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["env", "add", name, targetEnvironment];
        AddOptionalScopeArgument(arguments, options);
        // Vercel env commands must run inside a linked project directory. Use the
        // Aspire-owned scratch link instead of the source root so provider metadata
        // and pulled env files never mutate the user's checkout.
        // --sensitive ensures the value is stored in Vercel's project env store instead
        // of being visible in generated Build Output or deploy command arguments.
        // See https://vercel.com/docs/cli/env and https://vercel.com/docs/environment-variables/sensitive-environment-variables.
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--force");
        arguments.Add("--sensitive");

        return RunVercelAsync(arguments, projectLinkDirectory, cancellationToken, standardInput: value);
    }

    public Task<VercelCliResult> RemoveProjectEnvironmentVariableAsync(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["env", "rm", name, targetEnvironment];
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");

        return RunVercelAsync(arguments, projectLinkDirectory, cancellationToken);
    }

    public Task<VercelCliResult> DeployPrebuiltAsync(
        VercelEnvironmentOptionsAnnotation options,
        string deployDirectory,
        string? projectNameOrId,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables,
        CancellationToken cancellationToken)
        => RunVercelAsync(BuildDeployPrebuiltArguments(options, deployDirectory, projectNameOrId, environmentVariables), deployDirectory, cancellationToken);

    public Task<VercelCliResult> InspectDeploymentAsync(
        VercelEnvironmentOptionsAnnotation options,
        string deploymentUrl,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["inspect", deploymentUrl];
        AddOptionalScopeArgument(arguments, options);
        // Deploy returning successfully only confirms submission. Ask inspect to wait for the
        // provider's final JSON state before deployment state is persisted.
        // See https://vercel.com/docs/cli/inspect.
        arguments.Add("--wait");
        arguments.Add("--timeout");
        arguments.Add("120s");
        arguments.Add("--format=json");

        return RunVercelAsync(arguments, workingDirectory: null, cancellationToken);
    }

    public Task<VercelCliResult> ValidateDockerBuildxAsync(CancellationToken cancellationToken)
        => RunDockerAsync(["buildx", "version"], workingDirectory: null, cancellationToken);

    public Task<VercelCliResult> LoginToVcrAsync(string ownerId, string oidcToken, CancellationToken cancellationToken)
        => RunDockerAsync(["login", VercelConstants.VcrRegistry, "--username", ownerId, "--password-stdin"], workingDirectory: null, cancellationToken, standardInput: oidcToken);

    public Task<VercelCliResult> InspectDockerImageDigestAsync(string imageReference, CancellationToken cancellationToken)
        // Inspect the pushed tag through Docker buildx so deploy can pin the Vercel Build
        // Output API container handler to the linux/amd64 manifest digest Vercel accepts.
        // See https://vercel.com/docs/functions/container-images.
        => RunDockerAsync(BuildDockerInspectDigestArguments(imageReference), workingDirectory: null, cancellationToken);

    internal static string[] BuildDeployPrebuiltArgumentsForPlan(
        VercelEnvironmentOptionsAnnotation options,
        string deployDirectory,
        string? projectNameOrId,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
        => BuildDeployPrebuiltArguments(options, deployDirectory, projectNameOrId, environmentVariables);

    internal static string BuildDisplayDeployCommand(
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        string serviceName,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        // The plan should explain the command shape without leaking concrete source roots,
        // project names, or environment values that may be machine- or account-specific.
        var displayEnvironmentVariables = environmentVariables
            .Select(static environmentVariable => new KeyValuePair<string, string>(environmentVariable.Key, "<value>"))
            .ToArray();

        string displayImage = $"{VercelConstants.VcrRegistry}/<owner>/<project>/{serviceName}:<tag>";
        string displayDeployDirectory = $"<{resourceName}-build-output>";
        string displayProject = $"<{resourceName}-vercel-project>";
        return $"vercel pull --cwd <{resourceName}-vercel-project-link> --yes --environment {VercelProjectEnvironment.GetName(options)} && aspire build/push {resourceName} -> {displayImage} && docker {string.Join(" ", BuildDockerInspectDigestArguments(displayImage))} && vercel {string.Join(" ", BuildDeployPrebuiltArguments(options, displayDeployDirectory, displayProject, displayEnvironmentVariables))}";
    }

    private Task<VercelCliResult> RunVercelAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        string? standardInput = null)
        => _processRunner(VercelConstants.CliFileName, arguments, workingDirectory, cancellationToken, standardInput);

    private Task<VercelCliResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        string? standardInput = null)
        => _processRunner(VercelConstants.DockerCliFileName, arguments, workingDirectory, cancellationToken, standardInput);

    private static string[] BuildDockerInspectDigestArguments(string imageReference)
        => ["buildx", "imagetools", "inspect", "--format", "{{json .Manifest}}", imageReference];

    private static string[] BuildDeployPrebuiltArguments(
        VercelEnvironmentOptionsAnnotation options,
        string deployDirectory,
        string? projectNameOrId,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        List<string> arguments = ["deploy"];
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(deployDirectory);
        AddOptionalProjectArgument(arguments, projectNameOrId);
        // --prebuilt tells the CLI to upload the generated .vercel/output tree rather than
        // running a source build. That tree is produced by VercelBuildOutputWriter.
        // See https://vercel.com/docs/cli/deploy and https://vercel.com/docs/build-output-api.
        arguments.Add("--prebuilt");
        arguments.Add("--yes");

        if (options.Production)
        {
            arguments.Add("--prod");
        }

        if (!string.IsNullOrWhiteSpace(options.Target))
        {
            arguments.Add("--target");
            arguments.Add(options.Target);
        }

        foreach (var environmentVariable in environmentVariables.OrderBy(static variable => variable.Key, StringComparer.Ordinal))
        {
            arguments.Add("--env");
            arguments.Add($"{environmentVariable.Key}={environmentVariable.Value}");
        }

        return [.. arguments];
    }

    private static void AddOptionalProjectArgument(List<string> arguments, string? projectNameOrId)
    {
        if (!string.IsNullOrWhiteSpace(projectNameOrId))
        {
            arguments.Add("--project");
            arguments.Add(projectNameOrId);
        }
    }

    private static void AddOptionalScopeArgument(List<string> arguments, VercelEnvironmentOptionsAnnotation options)
    {
        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }
    }

    private static async Task<VercelCliResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken,
        string? standardInput)
    {
        // Keep all target CLI calls behind this runner so unit tests can assert exact
        // executable/argument/stdin boundaries. Secrets use stdin; arguments are never
        // shell-concatenated, which avoids platform quoting differences.
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        // Vercel CLI can emit ANSI color codes that make parser failure messages harder to
        // read and snapshot. Disable them at the process boundary for deterministic output.
        startInfo.Environment["NO_COLOR"] = "1";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new DistributedApplicationException($"The Vercel CLI process '{fileName}' could not be started.");

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);

            return new VercelCliResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Win32Exception ex)
        {
            throw new DistributedApplicationException(
                $"The Vercel CLI '{fileName}' could not be started. Install Vercel CLI from https://vercel.com/docs/cli and ensure it is available on PATH.",
                ex);
        }
    }
}

/// <summary>
/// Captures a completed CLI invocation so callers can decide whether to parse stdout,
/// surface stderr, or combine both for diagnostics.
/// </summary>
internal sealed record VercelCliResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
