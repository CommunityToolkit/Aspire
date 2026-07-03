namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelCliArguments
{
    private const string VcrRegistry = "vcr.vercel.com";
    private const string VercelContainerServiceName = "app";

    public static string[] BuildDeployArguments(VercelEnvironmentOptionsAnnotation options, VercelDeploymentEntry entry)
        => BuildDeployArguments(options, VercelDeploymentPaths.GetDeployDirectory(entry), VercelProjectNameResolver.GetProjectOption(entry), environmentVariables: []);

    public static string[] BuildDockerInspectDigestArguments(string imageReference)
        => ["buildx", "imagetools", "inspect", "--format", "{{json .Manifest}}", imageReference];

    public static string[] BuildDestroyProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("remove");
        arguments.Add(projectName);
        AddOptionalScopeArgument(arguments, options);

        return [.. arguments];
    }

    public static string[] BuildListProjectEnvironmentVariablesArguments(
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

    public static string[] BuildAddProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("add");
        arguments.Add(projectName);
        AddOptionalScopeArgument(arguments, options);

        return [.. arguments];
    }

    public static string[] BuildAddProjectEnvironmentVariableArguments(
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

    public static string[] BuildRemoveProjectEnvironmentVariableArguments(
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

    public static string[] BuildLinkProjectArguments(
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

    public static string[] BuildPullProjectSettingsArguments(
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

    public static string[] BuildDockerLoginArguments(string username)
        => ["login", VcrRegistry, "--username", username, "--password-stdin"];

    public static string[] BuildInspectDeploymentArguments(VercelEnvironmentOptionsAnnotation options, string deploymentUrl)
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

    public static string[] BuildValidateScopeArguments(VercelEnvironmentOptionsAnnotation options)
        => BuildListProjectsArguments(options);

    public static string[] BuildListProjectsArguments(VercelEnvironmentOptionsAnnotation options, string? filter = null)
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

    public static string[] BuildDeployArguments(
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

    public static string BuildDisplayDeployCommand(
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
        return $"vercel pull --cwd <{resourceName}-vercel-project-link> --yes --environment {VercelProjectEnvironment.GetName(options)} && aspire build/push {resourceName} -> {displayImage} && docker {string.Join(" ", BuildDockerInspectDigestArguments(displayImage))} && vercel {string.Join(" ", BuildDeployArguments(options, displayDeployDirectory, displayProject, displayEnvironmentVariables))}";
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
}
