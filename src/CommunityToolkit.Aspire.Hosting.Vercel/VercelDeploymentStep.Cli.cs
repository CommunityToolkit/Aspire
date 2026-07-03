#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES003
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPROBES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static partial class VercelDeploymentStep
{
    public static async Task ValidateCliPrerequisitesAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();

        var versionResult = await runner.RunAsync(VercelCliFileName, ["--version"], workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!versionResult.Succeeded)
        {
            throw CreateCliException("validate Vercel CLI installation", VercelCliFileName, versionResult);
        }

        var versionOutput = $"{versionResult.StandardOutput}{Environment.NewLine}{versionResult.StandardError}";
        if (!TryGetVercelCliVersion(versionOutput, out var version))
        {
            throw new DistributedApplicationException(
                $"Failed to determine Vercel CLI version from '{GetTrimmedOutput(versionOutput)}'. Install Vercel CLI {MinimumVercelCliVersion} or later from https://vercel.com/docs/cli.");
        }

        // The preview relies on newer CLI behavior: project-scoped link/pull,
        // prebuilt deploys, deployment-scoped --env, JSON inspect output with
        // --wait/--timeout, and project removal.
        if (version < MinimumVercelCliVersion)
        {
            throw new DistributedApplicationException(
                $"Vercel CLI version '{version}' is not supported. Install Vercel CLI {MinimumVercelCliVersion} or later from https://vercel.com/docs/cli.");
        }

        var whoamiResult = await runner.RunAsync(VercelCliFileName, ["whoami"], workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!whoamiResult.Succeeded)
        {
            throw CreateCliException("validate Vercel authentication", VercelCliFileName, whoamiResult);
        }

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            var scopeResult = await runner.RunAsync(VercelCliFileName, BuildValidateScopeArguments(options), workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
            if (!scopeResult.Succeeded)
            {
                throw CreateCliException($"validate Vercel scope '{options.Scope}'", VercelCliFileName, scopeResult);
            }
        }
    }

    internal static string[] BuildDeployArguments(VercelEnvironmentOptionsAnnotation options, VercelDeploymentEntry entry)
        => BuildDeployArguments(options, GetDeployDirectory(entry), GetVercelProjectOption(entry), environmentVariables: []);

    // Keep CLI argument construction as pure array-returning helpers. Tests assert exact
    // argument boundaries so Vercel quirks such as `env add` requiring --cwd, not --project,
    // cannot regress into shell-quoted or source-mutating command strings.
    internal static string[] BuildDockerInspectDigestArguments(string imageReference)
        => ["buildx", "imagetools", "inspect", "--format", "{{json .Manifest}}", imageReference];

    internal static string[] BuildDestroyProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("remove");
        arguments.Add(projectName);
        AddOptionalScopeArgument(arguments, options);

        return [.. arguments];
    }

    internal static string[] BuildListProjectEnvironmentVariablesArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("env");
        arguments.Add("ls");
        arguments.Add(targetEnvironment);
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--format=json");

        return [.. arguments];
    }

    internal static string[] BuildAddProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("add");
        arguments.Add(projectName);
        AddOptionalScopeArgument(arguments, options);

        return [.. arguments];
    }

    internal static string[] BuildAddProjectEnvironmentVariableArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("env");
        arguments.Add("add");
        arguments.Add(name);
        arguments.Add(targetEnvironment);
        AddOptionalScopeArgument(arguments, options);
        // Vercel env commands must run inside a linked project directory. Use the
        // Aspire-owned scratch link instead of the source root so provider metadata
        // and pulled env files never mutate the user's checkout.
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--force");
        arguments.Add("--sensitive");

        return [.. arguments];
    }

    internal static string[] BuildRemoveProjectEnvironmentVariableArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("env");
        arguments.Add("rm");
        arguments.Add(name);
        arguments.Add(targetEnvironment);
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");

        return [.. arguments];
    }

    internal static string[] BuildLinkProjectArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string projectNameOrId)
    {
        List<string> arguments = [];

        arguments.Add("link");
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--project");
        arguments.Add(projectNameOrId);

        return [.. arguments];
    }

    internal static string[] BuildPullProjectSettingsArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("pull");
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--environment");
        arguments.Add(targetEnvironment);

        return [.. arguments];
    }

    internal static string[] BuildDockerLoginArguments(string username)
        => ["login", VcrRegistry, "--username", username, "--password-stdin"];

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

    internal static string[] BuildInspectDeploymentArguments(VercelEnvironmentOptionsAnnotation options, string deploymentUrl)
    {
        List<string> arguments = [];

        arguments.Add("inspect");
        arguments.Add(deploymentUrl);
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--wait");
        arguments.Add("--timeout");
        arguments.Add("120s");
        arguments.Add("--format=json");

        return [.. arguments];
    }

    internal static string[] BuildValidateScopeArguments(VercelEnvironmentOptionsAnnotation options)
        => BuildListProjectsArguments(options);

    internal static string[] BuildListProjectsArguments(VercelEnvironmentOptionsAnnotation options, string? filter = null)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("ls");
        AddOptionalScopeArgument(arguments, options);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            arguments.Add("--filter");
            arguments.Add(filter);
        }

        arguments.Add("--format=json");

        return [.. arguments];
    }

    private static async Task VerifyDeploymentAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        VercelDeploymentResult deploymentResult)
    {
        // A successful `vercel deploy` only means the CLI accepted the submission.
        // Query the provider before recording state so Aspire does not persist failed
        // or still-building deployments as successfully applied resources.
        string[] arguments = BuildInspectDeploymentArguments(options, deploymentResult.DeploymentUrl);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw CreateCliException($"verify Vercel deployment for resource '{resourceName}'", VercelCliFileName, result);
        }

        var inspection = GetDeploymentInspection(result.StandardOutput);
        if (inspection.ReadyState is null)
        {
            throw new DistributedApplicationException($"Vercel inspect output for resource '{resourceName}' did not include a deployment ready state. Output: {GetTrimmedOutput(result.StandardOutput)}");
        }

        if (!string.Equals(inspection.ReadyState, "READY", StringComparison.OrdinalIgnoreCase))
        {
            throw new DistributedApplicationException($"Vercel deployment for resource '{resourceName}' finished with state '{inspection.ReadyState}' instead of 'READY'.");
        }
    }


    private static string[] BuildDeployArguments(
        VercelEnvironmentOptionsAnnotation options,
        string deployDirectory,
        string? projectNameOrId,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        List<string> arguments = [];

        arguments.Add("deploy");
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(deployDirectory);
        AddOptionalProjectArgument(arguments, projectNameOrId);
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

    private static string BuildDisplayDeployCommand(
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        // The plan should explain the command shape without leaking concrete source roots,
        // project names, or environment values that may be machine- or account-specific.
        var displayEnvironmentVariables = environmentVariables
            .Select(static environmentVariable => new KeyValuePair<string, string>(environmentVariable.Key, "<value>"))
            .ToArray();

        string displayImage = $"vcr.vercel.com/<owner>/<project>/{VercelContainerServiceName}:<tag>";
        string displayDeployDirectory = $"<{resourceName}-build-output>";
        string displayProject = $"<{resourceName}-vercel-project>";
        return $"vercel pull --cwd <{resourceName}-vercel-project-link> --yes --environment {GetVercelProjectEnvironmentName(options)} && aspire build/push {resourceName} -> {displayImage} && docker {string.Join(" ", BuildDockerInspectDigestArguments(displayImage))} && vercel {string.Join(" ", BuildDeployArguments(options, displayDeployDirectory, displayProject, displayEnvironmentVariables))}";
    }


    internal static string GetDeploymentUrl(string standardOutput)
        => GetDeploymentResult(standardOutput).DeploymentUrl;

    internal static VercelDeploymentResult GetDeploymentResult(string standardOutput)
    {
        // `vercel deploy` output has changed between CLI versions and flags. Prefer structured
        // JSON when present, then fall back to the last plain HTTP(S) URL printed by the CLI.
        if (TryGetJsonDeploymentResult(standardOutput) is { } jsonDeploymentResult)
        {
            return jsonDeploymentResult;
        }

        // Older CLI versions printed the deployment URL as plain text. Keep the fallback so
        // we fail only when no usable URL exists, not because formatting changed slightly.
        string[] lines = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string? deploymentUrl = lines.LastOrDefault(IsHttpUrl);
        if (deploymentUrl is null)
        {
            throw new DistributedApplicationException($"Vercel deploy output did not contain an HTTP or HTTPS deployment URL. Output: {GetTrimmedOutput(standardOutput)}");
        }

        return new(DeploymentId: null, deploymentUrl);
    }

    internal static VercelDeploymentInspection GetDeploymentInspection(string standardOutput)
    {
        try
        {
            // Parse the Vercel inspect JSON shapes observed across CLI versions:
            //   { "readyState": "READY" }
            //   { "state": "READY" }
            //   { "deployment": { "readyState": "READY" } }
            //   { "deployment": { "state": "READY" } }
            using var document = JsonDocument.Parse(standardOutput);
            var root = document.RootElement;
            string? readyState = GetJsonStringProperty(root, "readyState")
                ?? GetJsonStringProperty(root, "state")
                ?? (root.TryGetProperty("deployment", out var deployment) ? GetJsonStringProperty(deployment, "readyState") : null)
                ?? (root.TryGetProperty("deployment", out deployment) ? GetJsonStringProperty(deployment, "state") : null);

            return new(readyState);
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException("Failed to parse JSON output from 'vercel inspect'.", ex);
        }
    }

    private static VercelDeploymentResult? TryGetJsonDeploymentResult(string standardOutput)
    {
        if (!standardOutput.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            var root = document.RootElement;

            if (TryGetDeploymentResult(root, out var deploymentResult))
            {
                return deploymentResult;
            }
        }
        catch (JsonException)
        {
            // Older Vercel CLI output is plain text; fall back to line-based URL extraction.
        }

        return null;
    }

    private static bool TryGetDeploymentResult(JsonElement root, [NotNullWhen(true)] out VercelDeploymentResult? deploymentResult)
    {
        // Parse the Vercel deploy JSON shapes observed from different CLI versions:
        //   { "deployment": { "url": "https://...", "id": "..." } }
        //   { "url": "https://...", "id": "..." }
        // Callers fall back to line-based extraction when deploy output is plain text.
        if (root.TryGetProperty("deployment", out var deployment)
            && deployment.TryGetProperty("url", out var nestedUrl)
            && TryGetHttpUrl(nestedUrl, out var nestedDeploymentUrl))
        {
            string? deploymentId = deployment.TryGetProperty("id", out var nestedId) && nestedId.ValueKind == JsonValueKind.String
                ? nestedId.GetString()
                : null;

            deploymentResult = new(deploymentId, nestedDeploymentUrl);
            return true;
        }

        if (root.TryGetProperty("url", out var url)
            && TryGetHttpUrl(url, out var rootDeploymentUrl))
        {
            string? deploymentId = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;

            deploymentResult = new(deploymentId, rootDeploymentUrl);
            return true;
        }

        deploymentResult = null;
        return false;
    }

    private static bool TryGetHttpUrl(JsonElement urlElement, [NotNullWhen(true)] out string? url)
    {
        url = urlElement.ValueKind == JsonValueKind.String
            ? urlElement.GetString()
            : null;

        return IsHttpUrl(url);
    }

    private static bool IsHttpUrl([NotNullWhen(true)] string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";

    internal static bool TryGetVercelCliVersion(string output, [NotNullWhen(true)] out Version? version)
    {
        // The CLI can print banners/warnings around the version. Extract the first semantic
        // x.y.z token instead of requiring a line to be exactly the version string.
        var match = Regex.Match(output, @"(?<!\d)(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success)
        {
            version = null;
            return false;
        }

        version = new(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private static string GetTrimmedOutput(string output)
        => string.IsNullOrWhiteSpace(output) ? "<empty>" : output.Trim();

    private static DistributedApplicationException CreateCliException(string operation, string cliPath, VercelCliResult result)
    {
        string output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return new DistributedApplicationException($"Failed to {operation} using '{cliPath}' (exit code {result.ExitCode}). {output}");
    }
}
