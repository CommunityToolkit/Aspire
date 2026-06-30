#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelDeploymentStep
{
    public const string PublishStepNamePrefix = "vercel-publish-";
    public const string DeployPrereqStepNamePrefix = "vercel-deploy-prereq-";
    public const string DeployStepNamePrefix = "vercel-deploy-";
    public const string DestroyPrereqStepNamePrefix = "vercel-destroy-prereq-";
    public const string DestroyStepNamePrefix = "vercel-destroy-";
    public const string DeploymentPlanFileName = "vercel-deployments.json";

    private const string StateSectionNamePrefix = "communitytoolkit.vercel.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task WriteDeploymentPlanAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string outputDirectory = outputService.GetOutputDirectory(environment);

        string planPath = await WriteDeploymentPlanAsync(
            context.ExecutionContext,
            context.Logger,
            context.Model,
            environment,
            outputDirectory,
            context.CancellationToken).ConfigureAwait(false);

        context.Summary.Add("Vercel deployment plan", planPath);
    }

    internal static async Task<string> WriteDeploymentPlanAsync(
        DistributedApplicationModel model,
        VercelEnvironmentResource environment,
        string outputDirectory,
        CancellationToken cancellationToken)
        => await WriteDeploymentPlanAsync(
            executionContext: null,
            logger: null,
            model,
            environment,
            outputDirectory,
            cancellationToken).ConfigureAwait(false);

    internal static async Task<string> WriteDeploymentPlanAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        DistributedApplicationModel model,
        VercelEnvironmentResource environment,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var entries = GetDeploymentEntries(model, environment).ToList();
        ValidateEntries(entries);

        Directory.CreateDirectory(outputDirectory);
        var options = environment.GetVercelOptions();

        var plan = new VercelDeploymentPlan(
            environment.Name,
            await CreateDeploymentPlanEntriesAsync(
                executionContext,
                logger,
                options,
                entries,
                cancellationToken).ConfigureAwait(false));

        string planPath = Path.Combine(outputDirectory, DeploymentPlanFileName);
        await using FileStream stream = File.Create(planPath);
        await JsonSerializer.SerializeAsync(stream, plan, JsonOptions, cancellationToken).ConfigureAwait(false);

        return planPath;
    }

    public static async Task ValidatePrerequisitesAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        await ValidateCliPrerequisitesAsync(context, environment).ConfigureAwait(false);

        var entries = GetDeploymentEntries(context.Model, environment).ToList();
        ValidateEntries(entries);
    }

    public static async Task ValidateCliPrerequisitesAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();

        var versionResult = await runner.RunAsync(options.CliPath, ["--version"], workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!versionResult.Succeeded)
        {
            throw CreateCliException("validate Vercel CLI installation", options.CliPath, versionResult);
        }

        var whoamiResult = await runner.RunAsync(options.CliPath, ["whoami"], workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!whoamiResult.Succeeded)
        {
            throw CreateCliException("validate Vercel authentication", options.CliPath, whoamiResult);
        }
    }

    public static async Task DeployAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var entries = GetDeploymentEntries(context.Model, environment).ToList();

        ValidateEntries(entries);

        List<VercelDeploymentStateEntry> stateEntries = [];

        foreach (var entry in entries)
        {
            var preparedEntry = await PrepareDeploymentEntryAsync(context, entry).ConfigureAwait(false);
            string[] arguments = await BuildDeployArgumentsAsync(
                context.ExecutionContext,
                context.Logger,
                options,
                preparedEntry,
                context.CancellationToken).ConfigureAwait(false);

            var result = await runner.RunAsync(options.CliPath, arguments, preparedEntry.SourceRoot, context.CancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw CreateCliException($"deploy resource '{entry.Resource.Name}' to Vercel", options.CliPath, result);
            }

            var deploymentResult = GetDeploymentResult(result.StandardOutput);
            string projectName = GetVercelProjectName(preparedEntry);

            stateEntries.Add(new(
                entry.Resource.Name,
                projectName,
                deploymentResult.DeploymentId,
                deploymentResult.DeploymentUrl));

            context.Summary.Add($"{entry.Resource.Name} Vercel deployment", deploymentResult.DeploymentUrl);
        }

        await SaveDeploymentStateAsync(context, environment, stateEntries).ConfigureAwait(false);
    }

    internal static string[] BuildDeployArguments(VercelEnvironmentOptionsAnnotation options, VercelDeploymentEntry entry)
        => BuildDeployArguments(options, entry.SourceRoot, environmentVariables: []);

    internal static string[] BuildDestroyProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }

        arguments.Add("project");
        arguments.Add("remove");
        arguments.Add(projectName);

        return [.. arguments];
    }

    public static async Task DestroyAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        VercelDeploymentState state = ReadDeploymentState(stateSection) ?? GetFallbackDeploymentState(context.Model, environment);
        var projects = state.Deployments
            .Select(static deployment => deployment.ProjectName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (projects.Length == 0)
        {
            context.Summary.Add("Vercel destroy", $"No Vercel deployments were found for environment '{environment.Name}'.");
            return;
        }

        foreach (string projectName in projects)
        {
            string[] arguments = BuildDestroyProjectArguments(options, projectName);
            var result = await runner.RunAsync(options.CliPath, arguments, workingDirectory: null, context.CancellationToken, standardInput: "y\n").ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw CreateCliException($"destroy Vercel project '{projectName}'", options.CliPath, result);
            }

            context.Summary.Add("Vercel project removed", projectName);
        }

        await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        CancellationToken cancellationToken)
    {
        var environmentVariables = await GetVercelEnvironmentVariablesAsync(
            executionContext,
            logger,
            entry,
            cancellationToken).ConfigureAwait(false);

        return BuildDeployArguments(options, entry.SourceRoot, environmentVariables);
    }

    private static async Task<VercelDeploymentPlanEntry[]> CreateDeploymentPlanEntriesAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        List<VercelDeploymentPlanEntry> planEntries = [];

        foreach (var entry in entries)
        {
            var environmentVariables = executionContext is null || logger is null
                ? []
                : await GetVercelEnvironmentVariablesAsync(executionContext, logger, entry, cancellationToken).ConfigureAwait(false);

            planEntries.Add(new(
                entry.Resource.Name,
                GetDisplayDockerfilePath(entry),
                BuildDisplayDeployCommand(options, entry.Resource.Name, environmentVariables),
                [.. environmentVariables.Select(static variable => variable.Key).Order(StringComparer.Ordinal)]));
        }

        return [.. planEntries];
    }

    private static async Task<IReadOnlyList<KeyValuePair<string, string>>> GetVercelEnvironmentVariablesAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelDeploymentEntry entry,
        CancellationToken cancellationToken)
    {
        var executionConfiguration = await ExecutionConfigurationBuilder
            .Create(entry.Resource)
            .WithEnvironmentVariablesConfig()
            .WithArgumentsConfig()
            .BuildAsync(executionContext, logger, cancellationToken)
            .ConfigureAwait(false);

        if (executionConfiguration.Exception is not null)
        {
            throw new DistributedApplicationException($"Failed to process deployment configuration for resource '{entry.Resource.Name}'.", executionConfiguration.Exception);
        }

        ValidateUnsupportedRuntimeConfiguration(entry.Resource, executionConfiguration);

        var environmentVariables = GetVercelEnvironmentVariables(entry.Resource, executionConfiguration);

        return environmentVariables;
    }

    private static string[] BuildDeployArguments(
        VercelEnvironmentOptionsAnnotation options,
        string sourceRoot,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }

        arguments.Add("--cwd");
        arguments.Add(sourceRoot);
        arguments.Add("deploy");
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
        var displayEnvironmentVariables = environmentVariables
            .Select(static environmentVariable => new KeyValuePair<string, string>(environmentVariable.Key, "<value>"))
            .ToArray();

        return $"vercel {string.Join(" ", BuildDeployArguments(options, $"<{resourceName}-source-root>", displayEnvironmentVariables))}";
    }

    private static async Task SaveDeploymentStateAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        IReadOnlyList<VercelDeploymentStateEntry> deployments)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var state = new VercelDeploymentState(environment.Name, [.. deployments]);
        stateSection.SetValue(JsonSerializer.Serialize(state, JsonOptions));

        await stateManager.SaveSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
    }

    private static VercelDeploymentState? ReadDeploymentState(DeploymentStateSection stateSection)
    {
        if (!stateSection.Data.TryGetPropertyValue("value", out JsonNode? value)
            || value is null)
        {
            return null;
        }

        return value.GetValueKind() == JsonValueKind.String
            ? JsonSerializer.Deserialize<VercelDeploymentState>(value.GetValue<string>(), JsonOptions)
            : value.Deserialize<VercelDeploymentState>(JsonOptions);
    }

    private static VercelDeploymentState GetFallbackDeploymentState(DistributedApplicationModel model, VercelEnvironmentResource environment)
    {
        var deployments = GetDeploymentEntries(model, environment)
            .Select(static entry => new VercelDeploymentStateEntry(
                entry.Resource.Name,
                GetVercelProjectName(entry),
                DeploymentId: null,
                DeploymentUrl: null))
            .ToArray();

        return new(environment.Name, deployments);
    }

    private static string GetStateSectionName(VercelEnvironmentResource environment) => $"{StateSectionNamePrefix}{environment.Name}";

    private static IReadOnlyList<KeyValuePair<string, string>> GetVercelEnvironmentVariables(
        IResource resource,
        IExecutionConfigurationResult executionConfiguration)
    {
        List<KeyValuePair<string, string>> environmentVariables = [];

        foreach (var environmentVariable in executionConfiguration.EnvironmentVariablesWithUnprocessed)
        {
            string name = environmentVariable.Key;
            object unprocessedValue = environmentVariable.Value.Item1;
            string value = environmentVariable.Value.Item2;

            if (ContainsSecretReference(unprocessedValue))
            {
                throw new DistributedApplicationException(
                    $"Environment variable '{name}' for resource '{resource.Name}' references a secret or connection string. Vercel CLI --env would pass the value on the command line, so configure this value in Vercel project environment variables or a Vercel secret instead.");
            }

            environmentVariables.Add(new(name, value));
        }

        return environmentVariables;
    }

    private static void ValidateUnsupportedRuntimeConfiguration(
        IResource resource,
        IExecutionConfigurationResult executionConfiguration)
    {
        if (resource is ContainerResource { Entrypoint: not null })
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures a container entrypoint, but Vercel Dockerfile deployments use the CMD/ENTRYPOINT from the Dockerfile. Move the entrypoint into the Dockerfile.");
        }

        if (executionConfiguration.ArgumentsWithUnprocessed.Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire command-line arguments, but Vercel Dockerfile deployments cannot override Docker CMD/ENTRYPOINT. Move these arguments into the Dockerfile or express them as environment variables.");
        }

        if (resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile)
            && (dockerfile.BuildArguments.Count > 0 || dockerfile.BuildSecrets.Count > 0))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire Docker build arguments or build secrets. Vercel builds the Dockerfile itself, so configure build-time values in Vercel instead.");
        }
    }

    private static bool ContainsSecretReference(object? value)
    {
        return value switch
        {
            null => false,
            string => false,
            ParameterResource parameter => parameter.Secret,
            IResourceBuilder<ParameterResource> parameterBuilder => parameterBuilder.Resource.Secret,
            IResourceWithConnectionString => true,
            IResourceBuilder<IResourceWithConnectionString> => true,
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(ContainsSecretReference),
            _ => IsConnectionStringResourceBuilder(value)
        };
    }

    private static bool IsConnectionStringResourceBuilder(object value)
    {
        return value.GetType()
            .GetInterfaces()
            .Any(static interfaceType =>
                interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IResourceBuilder<>)
                && typeof(IResourceWithConnectionString).IsAssignableFrom(interfaceType.GetGenericArguments()[0]));
    }

    internal static IEnumerable<VercelDeploymentEntry> GetDeploymentEntries(DistributedApplicationModel model, VercelEnvironmentResource environment)
    {
        foreach (var resource in model.Resources)
        {
            if (resource.GetComputeEnvironment() != environment)
            {
                continue;
            }

            if (!resource.TryGetLastAnnotation<VercelDeploymentAnnotation>(out _))
            {
                continue;
            }

            if (!resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile))
            {
                throw new DistributedApplicationException($"Resource '{resource.Name}' is published to Vercel but does not have Aspire Dockerfile build metadata. Configure the resource with PublishAsDockerFile, WithDockerfile, WithDockerfileFactory, or WithDockerfileBuilder before calling PublishAsVercel.");
            }

            yield return new(resource, dockerfile.ContextPath, dockerfile.DockerfilePath, dockerfile);
        }
    }

    private static void ValidateEntries(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        if (entries.Count == 0)
        {
            throw new DistributedApplicationException("No resources are configured for Vercel deployment. Call PublishAsVercel on at least one project, executable, or Dockerfile container resource.");
        }

        foreach (var entry in entries)
        {
            if (!Directory.Exists(entry.SourceRoot))
            {
                throw new DistributedApplicationException($"The Vercel source root '{entry.SourceRoot}' for resource '{entry.Resource.Name}' does not exist.");
            }

            if (entry.Dockerfile.DockerfileFactory is null && !File.Exists(entry.DockerfilePath))
            {
                throw new DistributedApplicationException($"The Vercel Dockerfile '{entry.DockerfilePath}' for resource '{entry.Resource.Name}' does not exist. Configure the resource with an existing Dockerfile or an Aspire generated Dockerfile before calling PublishAsVercel.");
            }
        }
    }

    private static async Task<VercelDeploymentEntry> PrepareDeploymentEntryAsync(PipelineStepContext context, VercelDeploymentEntry entry)
    {
        if (!RequiresStaging(entry))
        {
            return entry;
        }

        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string stagingRoot = GetStagingSourceRoot(outputService.GetTempDirectory(entry.Resource), entry);

        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        CopyDirectory(entry.SourceRoot, stagingRoot, context.CancellationToken);

        string stagedDockerfilePath = Path.Combine(stagingRoot, "Dockerfile");
        DockerfileFactoryContext dockerfileContext = new()
        {
            CancellationToken = context.CancellationToken,
            Resource = entry.Resource,
            Services = context.Services
        };

        await entry.Dockerfile.EmitDockerfileArtifactsAsync(dockerfileContext, stagedDockerfilePath).ConfigureAwait(false);

        return entry with
        {
            SourceRoot = stagingRoot,
            DockerfilePath = stagedDockerfilePath
        };
    }

    private static bool RequiresStaging(VercelDeploymentEntry entry)
    {
        if (entry.Dockerfile.DockerfileFactory is not null)
        {
            return true;
        }

        string dockerfileDirectory = Path.GetDirectoryName(Path.GetFullPath(entry.DockerfilePath)) ?? string.Empty;

        return !PathEquals(dockerfileDirectory, entry.SourceRoot)
            || !string.Equals(Path.GetFileName(entry.DockerfilePath), "Dockerfile", GetPathStringComparison());
    }

    private static string GetDisplayDockerfilePath(VercelDeploymentEntry entry)
    {
        if (RequiresStaging(entry))
        {
            return "Dockerfile";
        }

        return Path.GetRelativePath(entry.SourceRoot, entry.DockerfilePath);
    }

    private static string GetStagingSourceRoot(string tempDirectory, VercelDeploymentEntry entry)
    {
        string sourceRoot = Path.TrimEndingDirectorySeparator(entry.SourceRoot);
        string sourceRootName = Path.GetFileName(sourceRoot);

        if (string.IsNullOrWhiteSpace(sourceRootName))
        {
            sourceRootName = entry.Resource.Name;
        }

        return Path.Combine(tempDirectory, sourceRootName);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string destinationFile = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static bool PathEquals(string path, string otherPath)
        => string.Equals(Path.GetFullPath(path), Path.GetFullPath(otherPath), GetPathStringComparison());

    private static StringComparison GetPathStringComparison()
        => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    internal static string GetVercelProjectName(VercelDeploymentEntry entry)
    {
        string projectJsonPath = Path.Combine(entry.SourceRoot, ".vercel", "project.json");

        if (File.Exists(projectJsonPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(projectJsonPath));

            if (document.RootElement.TryGetProperty("projectName", out var projectName)
                && projectName.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(projectName.GetString()))
            {
                return projectName.GetString()!;
            }
        }

        string sourceRoot = Path.TrimEndingDirectorySeparator(entry.SourceRoot);
        string fallbackProjectName = Path.GetFileName(sourceRoot);
        if (string.IsNullOrWhiteSpace(fallbackProjectName))
        {
            throw new DistributedApplicationException($"Could not infer the Vercel project name for resource '{entry.Resource.Name}' from source root '{entry.SourceRoot}'.");
        }

        return fallbackProjectName;
    }

    internal static string GetDeploymentUrl(string standardOutput)
        => GetDeploymentResult(standardOutput).DeploymentUrl;

    internal static VercelDeploymentResult GetDeploymentResult(string standardOutput)
    {
        if (TryGetJsonDeploymentResult(standardOutput) is { } jsonDeploymentResult)
        {
            return jsonDeploymentResult;
        }

        string[] lines = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string deploymentUrl = lines.LastOrDefault(static line => Uri.TryCreate(line, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            ?? standardOutput.Trim();

        return new(DeploymentId: null, deploymentUrl);
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

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";
    }

    private static DistributedApplicationException CreateCliException(string operation, string cliPath, VercelCliResult result)
    {
        string output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return new DistributedApplicationException($"Failed to {operation} using '{cliPath}' (exit code {result.ExitCode}). {output}");
    }
}

internal sealed record VercelDeploymentEntry(IResource Resource, string SourceRoot, string DockerfilePath, DockerfileBuildAnnotation Dockerfile);

internal sealed record VercelDeploymentPlan(string Environment, VercelDeploymentPlanEntry[] Deployments);

internal sealed record VercelDeploymentPlanEntry(string ResourceName, string DockerfilePath, string DeployCommand, string[] EnvironmentVariables);

internal sealed record VercelDeploymentResult(string? DeploymentId, string DeploymentUrl);

internal sealed record VercelDeploymentState(string Environment, VercelDeploymentStateEntry[] Deployments);

internal sealed record VercelDeploymentStateEntry(string ResourceName, string ProjectName, string? DeploymentId, string? DeploymentUrl);
