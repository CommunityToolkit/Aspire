#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable ASPIREPROBES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting.Pipelines;
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

internal static class VercelDeploymentStep
{
    public const string PublishStepNamePrefix = "vercel-publish-";
    public const string DeployPrereqStepNamePrefix = "vercel-deploy-prereq-";
    public const string DeployStepNamePrefix = "vercel-deploy-";
    public const string DestroyPrereqStepNamePrefix = "vercel-destroy-prereq-";
    public const string DestroyStepNamePrefix = "vercel-destroy-";
    public const string DeploymentPlanFileName = "vercel-deployments.json";

    private const string StateSectionNamePrefix = "communitytoolkit.vercel.";
    private const int DeploymentStateSchemaVersion = 1;
    private const int VercelProjectNameMaxLength = 100;
    private const string VercelCliFileName = "vercel";
    private const string VercelDockerfileFileName = "Dockerfile.vercel";
    private const string VercelJsonFileName = "vercel.json";
    private const string VercelContainerServiceName = "app";
    private static readonly Version MinimumVercelCliVersion = new(54, 18, 6);
    // These keys either bypass services mode or configure build/runtime/env behavior that
    // Aspire must own so Dockerfile.vercel, endpoint refs, and secret handling stay coherent.
    private static readonly string[] VercelJsonServicesModeUnsupportedKeys =
    [
        "build",
        "builds",
        "buildCommand",
        "devCommand",
        "env",
        "framework",
        "functions",
        "ignoreCommand",
        "installCommand",
        "outputDirectory",
        "routes"
    ];

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

        // Publish is a reviewable handoff, not a dry-run deploy. Keep it deterministic:
        // show commands, Dockerfile paths, and env var names without resolving secrets or
        // depending on mutable Vercel state.
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

        // The preview relies on newer CLI behavior: deployment-scoped --env, JSON inspect
        // output with --wait/--timeout, project removal, and services-mode Dockerfile deploys.
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

    public static async Task DeployAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var entries = GetDeploymentEntries(context.Model, environment).ToList();

        ValidateEntries(entries);
        await ValidateExistingDeploymentStateAsync(context, environment, options).ConfigureAwait(false);
        var entriesByResourceName = GetDeploymentEntriesByResourceName(entries);

        foreach (var entry in entries)
        {
            var preparedEntry = await PrepareDeploymentEntryAsync(context, entry).ConfigureAwait(false);
            // A checked-in .vercel/project.json means the user linked an existing provider project,
            // so deploy can target it but destroy must not claim ownership of it.
            bool managedByAspire = !HasVercelProjectLinkFile(entry.SourceRoot);
            if (managedByAspire)
            {
                // Create/validate the project before env configuration. `vercel env add` is
                // project-scoped and does not accept --project, while deploy can pass --project.
                await EnsureManagedProjectAsync(context, runner, options, preparedEntry).ConfigureAwait(false);
            }

            // Use Aspire's unprocessed values so endpoint references and secrets keep their
            // graph meaning until Vercel-specific deployment translation happens.
            var environmentConfiguration = await GetVercelEnvironmentConfigurationAsync(
                context.ExecutionContext,
                context.Logger,
                options,
                preparedEntry,
                entriesByResourceName,
                resolveProjectEnvironmentVariableValues: true,
                context.CancellationToken).ConfigureAwait(false);
            // Vercel resolves project env vars during the provider-side build/runtime.
            // Configure secret-bearing values before deploy; non-secret per-deployment
            // values remain on `vercel deploy --env`.
            await ConfigureProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                preparedEntry,
                environmentConfiguration.ProjectEnvironmentVariables).ConfigureAwait(false);

            string[] arguments = BuildDeployArguments(
                options,
                preparedEntry.SourceRoot,
                GetVercelProjectName(preparedEntry),
                environmentConfiguration.DeploymentEnvironmentVariables);

            var result = await runner.RunAsync(VercelCliFileName, arguments, preparedEntry.SourceRoot, context.CancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw CreateCliException($"deploy resource '{entry.Resource.Name}' to Vercel", VercelCliFileName, result);
            }

            var deploymentResult = GetDeploymentResult(result.StandardOutput);
            await VerifyDeploymentAsync(context, runner, options, entry.Resource.Name, deploymentResult).ConfigureAwait(false);

            var projectLink = GetVercelProjectLink(preparedEntry);
            string? productionUrl = GetProductionUrl(options, projectLink.ProjectName);

            var stateEntry = new VercelDeploymentStateEntry(
                entry.Resource.Name,
                projectLink.ProjectName,
                projectLink.ProjectId,
                deploymentResult.DeploymentId,
                deploymentResult.DeploymentUrl,
                entry.SourceRoot,
                managedByAspire)
            {
                ProductionUrl = productionUrl
            };

            // Persist each verified resource independently. A later resource can fail after
            // Vercel has already created earlier projects, and destroy must still know which
            // managed projects are safe to remove or retry.
            await SaveDeploymentStateEntryAsync(context, environment, options, stateEntry).ConfigureAwait(false);

            context.Summary.Add($"{entry.Resource.Name} Vercel deployment", deploymentResult.DeploymentUrl);
            if (productionUrl is not null)
            {
                context.Summary.Add($"{entry.Resource.Name} Vercel production URL", productionUrl);
            }
        }
    }

    internal static string[] BuildDeployArguments(VercelEnvironmentOptionsAnnotation options, VercelDeploymentEntry entry)
        => BuildDeployArguments(options, entry.SourceRoot, GetVercelProjectName(entry), environmentVariables: []);

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

    internal static string[] BuildAddProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }

        arguments.Add("project");
        arguments.Add("add");
        arguments.Add(projectName);

        return [.. arguments];
    }

    internal static string[] BuildLinkProjectArguments(
        VercelEnvironmentOptionsAnnotation options,
        string sourceRoot,
        string projectName)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }

        arguments.Add("--cwd");
        arguments.Add(sourceRoot);
        arguments.Add("link");
        arguments.Add("--yes");
        arguments.Add("--project");
        arguments.Add(projectName);

        return [.. arguments];
    }

    internal static string[] BuildAddProjectEnvironmentVariableArguments(
        VercelEnvironmentOptionsAnnotation options,
        string sourceRoot,
        string name,
        string targetEnvironment)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }

        arguments.Add("--cwd");
        arguments.Add(sourceRoot);
        arguments.Add("env");
        arguments.Add("add");
        arguments.Add(name);
        arguments.Add(targetEnvironment);
        arguments.Add("--yes");
        arguments.Add("--force");
        arguments.Add("--sensitive");

        return [.. arguments];
    }

    internal static string[] BuildInspectDeploymentArguments(VercelEnvironmentOptionsAnnotation options, string deploymentUrl)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }

        arguments.Add("inspect");
        arguments.Add(deploymentUrl);
        arguments.Add("--wait");
        arguments.Add("--timeout");
        arguments.Add("120s");
        arguments.Add("--format=json");

        return [.. arguments];
    }

    internal static string[] BuildValidateScopeArguments(VercelEnvironmentOptionsAnnotation options)
    {
        List<string> arguments = [];

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }

        arguments.Add("project");
        arguments.Add("ls");
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

    public static async Task DestroyAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var state = ReadDeploymentState(stateSection);
        if (state is null)
        {
            // Keep no-op destroy cheap and offline. If Aspire has no deployment state,
            // there is no recorded provider object that this integration owns.
            context.Summary.Add("Vercel destroy", $"No Vercel deployment state was found for environment '{environment.Name}'. Nothing to destroy.");
            return;
        }

        ValidateDeploymentState(environment, options, state);

        // Destroy is state-first rather than model-first. The current AppHost may no
        // longer contain the resources that created these Vercel projects, but persisted
        // state records which provider objects Aspire is allowed to delete.
        var projects = state.Deployments
            .Where(static deployment => deployment.ManagedByAspire)
            .Select(static deployment => deployment.ProjectName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (projects.Length == 0)
        {
            // Linked .vercel projects are valid deploy targets but remain externally owned,
            // so clearing state is safer than deleting provider projects the user brought.
            context.Summary.Add("Vercel destroy", $"No Aspire-managed Vercel deployments were found for environment '{environment.Name}'.");
            await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // Validate auth only after we know there is provider state to mutate. This keeps
        // `aspire destroy` usable in clean workspaces or after state has already been removed.
        await ValidateCliPrerequisitesAsync(context, environment).ConfigureAwait(false);
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();

        foreach (string projectName in projects)
        {
            string[] arguments = BuildDestroyProjectArguments(options, projectName);
            var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken, standardInput: "y\n").ConfigureAwait(false);

            if (!result.Succeeded)
            {
                if (!IsMissingProjectResult(result, projectName))
                {
                    throw CreateCliException($"destroy Vercel project '{projectName}'", VercelCliFileName, result);
                }

                context.Summary.Add("Vercel project already absent", projectName);
            }
            else
            {
                context.Summary.Add("Vercel project removed", projectName);
            }

            state = RemoveManagedProjectFromDeploymentState(state, projectName);
            // Save after each project removal so a later CLI failure leaves retryable state
            // for projects that still exist instead of forgetting partially cleaned resources.
            stateSection.SetValue(JsonSerializer.Serialize(state, JsonOptions));
            await stateManager.SaveSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
        }

        await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        CancellationToken cancellationToken)
        => await BuildDeployArgumentsAsync(
            executionContext,
            logger,
            options,
            entry,
            [entry],
            cancellationToken).ConfigureAwait(false);

    internal static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        var entriesByResourceName = GetDeploymentEntriesByResourceName(entries);
        var environmentConfiguration = await GetVercelEnvironmentConfigurationAsync(
            executionContext,
            logger,
            options,
            entry,
            entriesByResourceName,
            resolveProjectEnvironmentVariableValues: false,
            cancellationToken).ConfigureAwait(false);

        return BuildDeployArguments(options, entry.SourceRoot, GetVercelProjectName(entry), environmentConfiguration.DeploymentEnvironmentVariables);
    }

    private static async Task<VercelDeploymentPlanEntry[]> CreateDeploymentPlanEntriesAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        List<VercelDeploymentPlanEntry> planEntries = [];
        var entriesByResourceName = GetDeploymentEntriesByResourceName(entries);

        foreach (var entry in entries)
        {
            // When execution context is available, include the same target-native env names
            // deploy will use. Values stay redacted because publish output is committed or
            // handed off more often than deploy logs.
            var environmentConfiguration = executionContext is null || logger is null
                ? VercelEnvironmentConfiguration.Empty
                : await GetVercelEnvironmentConfigurationAsync(executionContext, logger, options, entry, entriesByResourceName, resolveProjectEnvironmentVariableValues: false, cancellationToken).ConfigureAwait(false);

            planEntries.Add(new(
                entry.Resource.Name,
                GetDisplayDockerfilePath(entry),
                BuildDisplayDeployCommand(options, entry.Resource.Name, environmentConfiguration.DeploymentEnvironmentVariables),
                [.. environmentConfiguration.AllEnvironmentVariableNames.Order(StringComparer.Ordinal)]));
        }

        return [.. planEntries];
    }

    private static async Task<VercelEnvironmentConfiguration> GetVercelEnvironmentConfigurationAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        bool resolveProjectEnvironmentVariableValues,
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

        var environmentVariables = await GetVercelEnvironmentConfigurationAsync(
            entry.Resource,
            options,
            executionConfiguration,
            entriesByResourceName,
            resolveProjectEnvironmentVariableValues,
            cancellationToken).ConfigureAwait(false);

        return environmentVariables;
    }

    private static string[] BuildDeployArguments(
        VercelEnvironmentOptionsAnnotation options,
        string sourceRoot,
        string projectName,
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
        arguments.Add("--project");
        arguments.Add(projectName);

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

        return $"vercel {string.Join(" ", BuildDeployArguments(options, $"<{resourceName}-source-root>", projectName: $"<{resourceName}-project>", displayEnvironmentVariables))}";
    }

    private static async Task ValidateExistingDeploymentStateAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = ReadDeploymentState(stateSection);
        if (existingState is not null)
        {
            ValidateDeploymentState(environment, options, existingState);
        }
    }

    private static async Task SaveDeploymentStateEntryAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentStateEntry deployment)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = ReadDeploymentState(stateSection);
        var state = existingState is null
            ? CreateDeploymentState(environment, options, [deployment])
            : MergeDeploymentState(environment, options, existingState, deployment);

        stateSection.SetValue(JsonSerializer.Serialize(state, JsonOptions));

        await stateManager.SaveSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
    }

    private static VercelDeploymentState CreateDeploymentState(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentStateEntry[] deployments)
        => new(
            DeploymentStateSchemaVersion,
            environment.Name,
            NormalizeScope(options.Scope),
            NormalizeTarget(options.Target),
            options.Production,
            deployments);

    private static VercelDeploymentState MergeDeploymentState(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentState existingState,
        VercelDeploymentStateEntry deployment)
    {
        ValidateDeploymentState(environment, options, existingState);

        return CreateDeploymentState(
            environment,
            options,
            [
                .. existingState.Deployments.Where(existing =>
                    !string.Equals(existing.ResourceName, deployment.ResourceName, StringComparison.Ordinal)
                    || !string.Equals(existing.ProjectName, deployment.ProjectName, StringComparison.Ordinal)),
                deployment
            ]);
    }

    private static VercelDeploymentState? ReadDeploymentState(DeploymentStateSection stateSection)
    {
        // DeploymentStateSection storage shape has changed across Aspire builds. Accept the
        // known wrappers so destroy can still clean up projects created by an older CLI.
        if (stateSection.Data.TryGetPropertyValue("value", out JsonNode? value)
            && value is not null)
        {
            return DeserializeDeploymentState(value);
        }

        value = stateSection.Data.FirstOrDefault().Value;
        if (value is not null)
        {
            return DeserializeDeploymentState(value);
        }

        if (stateSection.Data.ContainsKey("schemaVersion"))
        {
            return stateSection.Data.Deserialize<VercelDeploymentState>(JsonOptions);
        }

        return null;
    }

    private static VercelDeploymentState? DeserializeDeploymentState(JsonNode value)
    {
        return value.GetValueKind() == JsonValueKind.String
            ? JsonSerializer.Deserialize<VercelDeploymentState>(value.GetValue<string>(), JsonOptions)
            : value.Deserialize<VercelDeploymentState>(JsonOptions);
    }

    private static void ValidateDeploymentState(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentState state)
    {
        if (state.SchemaVersion != DeploymentStateSchemaVersion)
        {
            throw new DistributedApplicationException($"Vercel deployment state for environment '{environment.Name}' uses unsupported schema version '{state.SchemaVersion}'. Redeploy the environment before destroying it.");
        }

        if (!string.Equals(state.Environment, environment.Name, StringComparison.Ordinal))
        {
            throw new DistributedApplicationException($"Vercel deployment state for environment '{state.Environment}' cannot be used to destroy environment '{environment.Name}'.");
        }

        string? configuredScope = NormalizeScope(options.Scope);
        if (!string.Equals(state.Scope, configuredScope, StringComparison.Ordinal))
        {
            string stateScope = string.IsNullOrWhiteSpace(state.Scope) ? "<default>" : state.Scope;
            string requestedScope = string.IsNullOrWhiteSpace(configuredScope) ? "<default>" : configuredScope;
            throw new DistributedApplicationException($"Vercel deployment state for environment '{environment.Name}' was created for scope '{stateScope}', but destroy is configured for scope '{requestedScope}'. Use the same Vercel scope that created the deployment state.");
        }
    }

    private static string? NormalizeScope(string? scope)
        => string.IsNullOrWhiteSpace(scope) ? null : scope;

    private static string? NormalizeTarget(string? target)
        => string.IsNullOrWhiteSpace(target) ? null : target;

    private static string? GetProductionUrl(VercelEnvironmentOptionsAnnotation options, string projectName)
        => options.Production ? $"https://{projectName}.vercel.app" : null;

    private static VercelDeploymentState RemoveManagedProjectFromDeploymentState(VercelDeploymentState state, string projectName)
        => state with
        {
            Deployments = state.Deployments
                .Where(deployment => !deployment.ManagedByAspire || !string.Equals(deployment.ProjectName, projectName, StringComparison.Ordinal))
                .ToArray()
        };

    private static bool IsMissingProjectResult(VercelCliResult result, string projectName)
    {
        string output = $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}";
        return output.Contains(projectName, StringComparison.OrdinalIgnoreCase)
            && (output.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || output.Contains("could not find", StringComparison.OrdinalIgnoreCase)
                || output.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                || output.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetStateSectionName(VercelEnvironmentResource environment) => $"{StateSectionNamePrefix}{environment.Name}";

    private static async Task ConfigureProjectEnvironmentVariablesAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        if (environmentVariables.Count == 0)
        {
            return;
        }

        string projectName = GetVercelProjectName(entry);
        if (!HasVercelProjectLinkFile(entry.SourceRoot))
        {
            // `vercel env add` does not accept --project, so link only the staged copy
            // before writing provider-managed secret values.
            string[] linkArguments = BuildLinkProjectArguments(options, entry.SourceRoot, projectName);
            var linkResult = await runner.RunAsync(VercelCliFileName, linkArguments, entry.SourceRoot, context.CancellationToken).ConfigureAwait(false);
            if (!linkResult.Succeeded)
            {
                throw CreateCliException($"link Vercel project '{projectName}' for resource '{entry.Resource.Name}'", VercelCliFileName, linkResult);
            }
        }

        string targetEnvironment = GetVercelProjectEnvironmentName(options);
        foreach (var environmentVariable in environmentVariables.OrderBy(static variable => variable.Key, StringComparer.Ordinal))
        {
            string[] arguments = BuildAddProjectEnvironmentVariableArguments(options, entry.SourceRoot, environmentVariable.Key, targetEnvironment);
            var result = await runner.RunAsync(VercelCliFileName, arguments, entry.SourceRoot, context.CancellationToken, standardInput: environmentVariable.Value).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw CreateCliException($"configure Vercel project environment variable '{environmentVariable.Key}' for resource '{entry.Resource.Name}'", VercelCliFileName, result);
            }
        }
    }

    private static async Task EnsureManagedProjectAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry)
    {
        string projectName = GetVercelProjectName(entry);
        string[] arguments = BuildAddProjectArguments(options, projectName);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"create or validate Vercel project '{projectName}' for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }
    }

    private static string GetVercelProjectEnvironmentName(VercelEnvironmentOptionsAnnotation options)
    {
        if (options.Production)
        {
            return "production";
        }

        return string.IsNullOrWhiteSpace(options.Target) ? "preview" : options.Target;
    }

    private static async Task<VercelEnvironmentConfiguration> GetVercelEnvironmentConfigurationAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IExecutionConfigurationResult executionConfiguration,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        bool resolveProjectEnvironmentVariableValues,
        CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> deploymentEnvironmentVariables = [];
        List<KeyValuePair<string, string>> projectEnvironmentVariables = [];
        HashSet<string> names = new(StringComparer.Ordinal);

        // This is the env-var edge-case boundary. Vercel has deployment env args and
        // project env secrets, but no Aspire-style connection binding/service-discovery
        // object in this preview. Only deterministic production endpoint URLs are projected;
        // other resource references fail here instead of becoming misleading strings.
        // Keep a single pass over Aspire's unprocessed environment dictionary. The processed
        // value can contain publish-mode manifest expressions, but deployment needs the
        // original graph value so it can choose Vercel's concrete URL/secret mechanism.
        foreach (var environmentVariable in executionConfiguration.EnvironmentVariablesWithUnprocessed)
        {
            string name = environmentVariable.Key;
            object unprocessedValue = environmentVariable.Value.Item1;
            string value = environmentVariable.Value.Item2;

            ValidateEnvironmentVariableName(resource, name);
            if (!names.Add(name))
            {
                throw new DistributedApplicationException(
                    $"Resource '{resource.Name}' configures environment variable '{name}' more than once. Vercel project environment variable names must be unique.");
            }

            if (ContainsUnsupportedResourceReference(resource, unprocessedValue))
            {
                throw new DistributedApplicationException(
                    $"Environment variable '{name}' for resource '{resource.Name}' references another Aspire resource or service in a way that cannot be represented as a Vercel deployment URL. Use endpoint references to Vercel production workloads, or configure the value in Vercel project environment variables.");
            }

            // Non-secrets can ride on `vercel deploy --env`; secret-bearing values must use
            // Vercel project environment variables so values never appear in CLI arguments.
            bool containsSecret = ContainsSecretReference(unprocessedValue);
            if (containsSecret)
            {
                value = resolveProjectEnvironmentVariableValues
                    ? await GetVercelProjectEnvironmentVariableValueAsync(
                        resource,
                        options,
                        entriesByResourceName,
                        unprocessedValue,
                        value,
                        cancellationToken).ConfigureAwait(false)
                    : "<value>";
            }
            else if (TryGetVercelEnvironmentVariableValue(resource, options, entriesByResourceName, unprocessedValue, out string? vercelValue))
            {
                value = vercelValue;
            }

            if (containsSecret)
            {
                projectEnvironmentVariables.Add(new(name, value));
            }
            else
            {
                deploymentEnvironmentVariables.Add(new(name, value));
            }
        }

        return new(deploymentEnvironmentVariables, projectEnvironmentVariables);
    }

    private static async ValueTask<string> GetVercelProjectEnvironmentVariableValueAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        object? value,
        string processedValue,
        CancellationToken cancellationToken)
    {
        // This path resolves values for `vercel env add`, not `vercel deploy --env`.
        // Values can be secret-bearing because they are sent on stdin to Vercel's secret
        // store; they must not be copied into publish plans, command lines, or state.
        switch (value)
        {
            case null:
                return processedValue;
            case string stringValue:
                return stringValue;
            case ParameterResource parameter:
                return await GetParameterValueAsync(parameter, cancellationToken).ConfigureAwait(false);
            case IResourceBuilder<ParameterResource> parameterBuilder:
                return await GetParameterValueAsync(parameterBuilder.Resource, cancellationToken).ConfigureAwait(false);
            case IResourceWithConnectionString connectionStringResource:
                return await GetValueProviderValueAsync(connectionStringResource.ConnectionStringExpression, $"connection string for resource '{connectionStringResource.Name}'", cancellationToken).ConfigureAwait(false);
            case IResourceBuilder<IResourceWithConnectionString> connectionStringBuilder:
                return await GetValueProviderValueAsync(connectionStringBuilder.Resource.ConnectionStringExpression, $"connection string for resource '{connectionStringBuilder.Resource.Name}'", cancellationToken).ConfigureAwait(false);
            case ReferenceExpression referenceExpression:
                return await GetVercelProjectReferenceExpressionValueAsync(resource, options, entriesByResourceName, referenceExpression, cancellationToken).ConfigureAwait(false);
            case IValueProvider valueProvider:
                return await GetValueProviderValueAsync(valueProvider, "environment variable value", cancellationToken).ConfigureAwait(false);
            default:
                return processedValue;
        }
    }

    private static async ValueTask<string> GetVercelProjectReferenceExpressionValueAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        ReferenceExpression referenceExpression,
        CancellationToken cancellationToken)
    {
        if (referenceExpression.IsConditional)
        {
            throw new DistributedApplicationException("Vercel project environment variables do not support conditional reference expressions. Configure a concrete Vercel project environment variable instead.");
        }

        var arguments = new object?[referenceExpression.ValueProviders.Count];
        for (int i = 0; i < referenceExpression.ValueProviders.Count; i++)
        {
            IValueProvider valueProvider = referenceExpression.ValueProviders[i];
            arguments[i] = valueProvider switch
            {
                // Secret-bearing project env vars may combine endpoint URLs with secret
                // parameters because the final value is sent through Vercel's secret path.
                EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url)),
                EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression),
                ParameterResource parameter => await GetParameterValueAsync(parameter, cancellationToken).ConfigureAwait(false),
                IResourceWithConnectionString connectionStringResource => await GetValueProviderValueAsync(connectionStringResource.ConnectionStringExpression, $"connection string for resource '{connectionStringResource.Name}'", cancellationToken).ConfigureAwait(false),
                _ => await GetValueProviderValueAsync(valueProvider, "reference expression value", cancellationToken).ConfigureAwait(false)
            };

            if (referenceExpression.StringFormats[i] is "uri" && arguments[i] is string stringValue)
            {
                arguments[i] = Uri.EscapeDataString(stringValue);
            }
        }

        return string.Format(CultureInfo.InvariantCulture, referenceExpression.Format, arguments);
    }

    private static async ValueTask<string> GetParameterValueAsync(ParameterResource parameter, CancellationToken cancellationToken)
    {
        try
        {
            string? value = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            return value ?? throw new DistributedApplicationException($"Secret parameter '{parameter.Name}' did not produce a value for Vercel project environment configuration.");
        }
        catch (MissingParameterValueException ex)
        {
            throw new DistributedApplicationException($"Secret parameter '{parameter.Name}' does not have a value. Provide a value before deploying to Vercel.", ex);
        }
    }

    private static async ValueTask<string> GetValueProviderValueAsync(IValueProvider valueProvider, string description, CancellationToken cancellationToken)
    {
        try
        {
            string? value = await valueProvider.GetValueAsync(cancellationToken).ConfigureAwait(false);
            return value ?? throw new DistributedApplicationException($"The {description} did not produce a value for Vercel project environment configuration.");
        }
        catch (MissingParameterValueException ex)
        {
            throw new DistributedApplicationException($"The {description} does not have a value. Provide a value before deploying to Vercel.", ex);
        }
    }

    private static bool TryGetVercelEnvironmentVariableValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        object? value,
        [NotNullWhen(true)] out string? vercelValue)
    {
        // Service-discovery env vars generated by WithReference also arrive as structured
        // endpoint values, so translate by value shape instead of by environment variable name.
        switch (value)
        {
            case EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference):
                vercelValue = GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url));
                return true;
            case EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint):
                vercelValue = GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression);
                return true;
            case ReferenceExpression referenceExpression when ContainsCrossResourceEndpointReference(resource, referenceExpression):
                vercelValue = GetVercelReferenceExpressionValue(resource, options, entriesByResourceName, referenceExpression);
                return true;
            default:
                vercelValue = null;
                return false;
        }
    }

    private static string GetVercelReferenceExpressionValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        ReferenceExpression referenceExpression)
    {
        if (referenceExpression.IsConditional)
        {
            throw new DistributedApplicationException("Vercel endpoint references do not support conditional reference expressions. Configure a concrete Vercel project environment variable instead.");
        }

        var arguments = new object?[referenceExpression.ValueProviders.Count];
        for (int i = 0; i < referenceExpression.ValueProviders.Count; i++)
        {
            IValueProvider valueProvider = referenceExpression.ValueProviders[i];
            arguments[i] = valueProvider switch
            {
                EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url)),
                EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression),
                // Mixed expressions can hide provider-specific ordering or secret semantics.
                // Keep this path to deterministic endpoint-only production URLs.
                _ => throw new DistributedApplicationException("Vercel endpoint reference expressions cannot be combined with parameters, secrets, or other value providers. Configure a concrete Vercel project environment variable instead.")
            };

            if (referenceExpression.StringFormats[i] is "uri" && arguments[i] is string stringValue)
            {
                arguments[i] = Uri.EscapeDataString(stringValue);
            }
        }

        return string.Format(CultureInfo.InvariantCulture, referenceExpression.Format, arguments);
    }

    private static string GetVercelEndpointPropertyValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        EndpointReferenceExpression endpointReferenceExpression)
    {
        // This is the endpoint-reference edge-case boundary: preview/custom URLs are
        // post-deploy outputs, internal endpoints have no public Vercel edge address, and
        // cross-environment references lack a stable same-deploy alias. Fail before values
        // are written to Vercel env vars.
        if (!options.Production)
        {
            throw new DistributedApplicationException(
                "Vercel endpoint references require production deployments because preview and custom target URLs are assigned by Vercel after deployment. Call WithVercelProductionDeployments on the Vercel environment, or remove the reference.");
        }

        var endpointReference = endpointReferenceExpression.Endpoint;
        var endpoint = endpointReference.EndpointAnnotation;
        if (!endpoint.IsExternal)
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but Vercel endpoint references can only target external HTTP or HTTPS endpoints. Configure an external endpoint or remove the reference.");
        }

        if (!IsHttpEndpoint(endpoint))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' with scheme '{endpoint.UriScheme}', but Vercel endpoint references support only HTTP or HTTPS endpoints.");
        }

        if (!entriesByResourceName.TryGetValue(endpointReference.Resource.Name, out var referencedEntry))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but the referenced resource does not target this Vercel environment. Vercel endpoint references can only target workloads deployed to the same Vercel environment.");
        }

        string host = $"{GetVercelProjectName(referencedEntry)}.vercel.app";
        const int port = 443;

        return endpointReferenceExpression.Property switch
        {
            // Production aliases are deterministic before deploy; preview/custom URLs are not.
            // Keep endpoint references on the stable caller-visible Vercel HTTPS surface.
            EndpointProperty.Url => $"https://{host}",
            EndpointProperty.Host or EndpointProperty.IPV4Host => host,
            EndpointProperty.Port => port.ToString(CultureInfo.InvariantCulture),
            EndpointProperty.TargetPort => endpoint.TargetPort is int targetPort
                ? targetPort.ToString(CultureInfo.InvariantCulture)
                : throw new DistributedApplicationException(
                    // Azure publishers can carry ContainerPortReference placeholders in
                    // Bicep/Helm. Vercel deploy receives concrete CLI env values, so an
                    // unresolved TargetPort would become a bogus string rather than a
                    // target-native reference.
                    $"Resource '{resource.Name}' references endpoint property '{EndpointProperty.TargetPort}' for endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but the endpoint does not define an explicit target port. Configure a target port or avoid passing TargetPort to Vercel."),
            EndpointProperty.Scheme => "https",
            EndpointProperty.HostAndPort => $"{host}:{port.ToString(CultureInfo.InvariantCulture)}",
            EndpointProperty.TlsEnabled => bool.TrueString,
            _ => throw new DistributedApplicationException($"The endpoint property '{endpointReferenceExpression.Property}' is not supported for Vercel endpoint references.")
        };
    }

    private static void ValidateEnvironmentVariableName(IResource resource, string name)
    {
        // Do not remap names to satisfy Vercel's env var shape. A lossy rename would break
        // the consuming workload's contract, so invalid names fail with the original key.
        if (string.IsNullOrWhiteSpace(name)
            || (!char.IsAsciiLetter(name[0]) && name[0] != '_')
            || name.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures invalid Vercel environment variable name '{name}'. Use letters, digits, and underscores, and start with a letter or underscore.");
        }
    }

    private static void ValidateUnsupportedRuntimeConfiguration(
        IResource resource,
        IExecutionConfigurationResult executionConfiguration)
    {
        // These Aspire concepts have no faithful Vercel Dockerfile-deploy equivalent in this
        // preview. Rejecting them is safer than silently dropping entrypoint, args, or build
        // values that would change the workload's deployed behavior. Vercel builds remotely
        // from uploaded source, so Aspire cannot apply late command/build overrides after upload.
        if (resource is ContainerResource { Entrypoint: not null })
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures a container entrypoint, but Vercel Dockerfile deployments use the CMD/ENTRYPOINT from Aspire's publish output. Configure the workload's publish behavior or Vercel project settings instead.");
        }

        if (executionConfiguration.ArgumentsWithUnprocessed.Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire command-line arguments, but Vercel Dockerfile deployments cannot override Docker CMD/ENTRYPOINT. Configure the workload's publish behavior or express the values as environment variables.");
        }

        if (resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile)
            && (dockerfile.BuildArguments.Count > 0 || dockerfile.BuildSecrets.Count > 0))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire Docker build arguments or build secrets. Vercel runs the Dockerfile build itself, so configure build-time values in Vercel instead.");
        }
    }

    private static bool ContainsSecretReference(object? value)
    {
        // Connection strings are treated as secret-bearing even when the underlying provider
        // does not mark each segment secret; Vercel should receive them through project env.
        return value switch
        {
            null => false,
            string => false,
            ParameterResource parameter => parameter.Secret,
            IResourceBuilder<ParameterResource> parameterBuilder => parameterBuilder.Resource.Secret,
            IResourceWithConnectionString => true,
            IResourceBuilder<IResourceWithConnectionString> => true,
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(ContainsSecretReference),
            _ => false
        };
    }

    private static bool ContainsCrossResourceEndpointReference(IResource resource, object? value)
    {
        return value switch
        {
            null => false,
            EndpointReference endpointReference => IsCrossResourceEndpointReference(resource, endpointReference),
            EndpointReferenceExpression endpointReferenceExpression => IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint),
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(reference => ContainsCrossResourceEndpointReference(resource, reference)),
            _ => false
        };
    }

    private static bool IsCrossResourceEndpointReference(IResource resource, EndpointReference endpointReference)
        => !IsSameResource(resource, endpointReference.Resource);

    private static bool ContainsUnsupportedResourceReference(IResource resource, object? value)
    {
        // Vercel only knows how to turn endpoint references into deterministic production
        // aliases. Other resource references can represent connection strings, parameters,
        // or custom values that need a target-native mechanism this preview does not have.
        return value switch
        {
            null => false,
            string => false,
            ParameterResource => false,
            IResourceBuilder<ParameterResource> => false,
            EndpointReference => false,
            EndpointReferenceExpression => false,
            IResource referencedResource => !IsSameResource(resource, referencedResource),
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(reference => ContainsUnsupportedResourceReference(resource, reference)),
            IResourceBuilder<IResource> resourceBuilder => !IsSameResource(resource, resourceBuilder.Resource),
            _ => false
        };
    }

    private static bool IsSameResource(IResource resource, IResource otherResource)
        // Compare by Aspire resource name rather than object identity. Polyglot/ATS flows
        // can recreate resource references across an RPC boundary, but resource name is the
        // app-model identity used by deployment target maps.
        => string.Equals(resource.Name, otherResource.Name, StringComparison.Ordinal);

    private static bool IsHttpEndpoint(EndpointAnnotation endpoint)
        // URI scheme alone is not enough: Aspire can model a TCP transport with an HTTP
        // URI scheme. Vercel's container ingress here is HTTP-family traffic over TCP.
        => endpoint.Protocol == ProtocolType.Tcp
            && (string.Equals(endpoint.UriScheme, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.UriScheme, "https", StringComparison.OrdinalIgnoreCase))
            && (string.Equals(endpoint.Transport, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.Transport, "http2", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, VercelDeploymentEntry> GetDeploymentEntriesByResourceName(IReadOnlyList<VercelDeploymentEntry> entries)
        => entries.ToDictionary(static entry => entry.Resource.Name, StringComparer.Ordinal);

    internal static IEnumerable<VercelDeploymentEntry> GetDeploymentEntries(DistributedApplicationModel model, VercelEnvironmentResource environment)
    {
        foreach (var resource in model.Resources.OfType<IComputeResource>())
        {
            if (!IsTargetedToEnvironment(resource, environment))
            {
                continue;
            }

            if (!resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile))
            {
                throw new DistributedApplicationException($"Resource '{resource.Name}' targets Vercel but does not have Aspire Dockerfile build metadata. Use a workload integration that publishes Dockerfile metadata, call PublishAsDockerFile, or configure the resource with WithDockerfile, WithDockerfileFactory, or WithDockerfileBuilder.");
            }

            yield return new(resource, dockerfile.ContextPath, dockerfile.DockerfilePath, dockerfile);
        }
    }

    private static bool IsTargetedToEnvironment(IResource resource, VercelEnvironmentResource environment)
    {
        var computeEnvironment = resource.GetComputeEnvironment();
        // Match Aspire's single-environment convention: when Vercel is the only compute
        // environment, Dockerfile-backed workloads implicitly target it.
        return computeEnvironment is null || ReferenceEquals(computeEnvironment, environment);
    }

    private static void ValidateEntries(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        // Keep unsupported Vercel-preview cases in validation so publish/prereq/deploy fail
        // before mutating provider projects. Each failure names the Aspire concept that
        // cannot be projected rather than letting a later Vercel CLI call fail opaquely.
        if (entries.Count == 0)
        {
            throw new DistributedApplicationException("No Dockerfile-backed compute resources target Vercel. Add a workload with Aspire Dockerfile publish metadata, or use WithComputeEnvironment to target Vercel when multiple compute environments are present.");
        }

        foreach (var entry in entries)
        {
            if (!Directory.Exists(entry.SourceRoot))
            {
                throw new DistributedApplicationException($"The Vercel source root '{entry.SourceRoot}' for resource '{entry.Resource.Name}' does not exist.");
            }

            if (entry.Dockerfile.DockerfileFactory is null && !File.Exists(entry.DockerfilePath))
            {
                throw new DistributedApplicationException($"The Vercel Dockerfile '{entry.DockerfilePath}' for resource '{entry.Resource.Name}' does not exist. Configure the resource with an existing Dockerfile or Aspire-generated Dockerfile metadata.");
            }

            ValidateUnsupportedResourceModel(entry);
        }

        ValidateUniqueVercelProjectNames(entries);
    }

    private static void ValidateUniqueVercelProjectNames(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        // Production endpoint references use https://{projectName}.vercel.app. If two
        // resources resolve to the same Vercel project, endpoint references and destroy
        // ownership would both become ambiguous.
        var projectNames = entries
            .Select(entry => new
            {
                Entry = entry,
                ProjectLink = GetVercelProjectLink(entry),
                Linked = HasVercelProjectLinkFile(entry.SourceRoot)
            })
            .GroupBy(item => item.ProjectLink.ProjectName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();

        if (projectNames.Length == 0)
        {
            return;
        }

        var collision = projectNames[0];
        string resources = string.Join(", ", collision.Select(static item => $"'{item.Entry.Resource.Name}'").Order(StringComparer.Ordinal));
        throw new DistributedApplicationException(
            $"Multiple Vercel resources resolve to project name '{collision.Key}' ({resources}). Vercel project names must be unique per environment because each resource deploys to and references one project production URL. Use WithVercelProjectName, distinct source directory names, or link each resource to a distinct Vercel project with .vercel/project.json.");
    }

    private static void ValidateUnsupportedResourceModel(VercelDeploymentEntry entry)
    {
        IResource resource = entry.Resource;

        // This method is intentionally conservative. Each rejected annotation has run-mode
        // or another deployment-target semantics that Vercel's Dockerfile deploy cannot
        // project without changing what the user modeled in the AppHost. If Vercel gains a
        // native equivalent later, add the mapping and tests here instead of silently ignoring it.
        if (resource.Annotations.OfType<ContainerMountAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire container volumes or bind mounts, but Vercel Dockerfile deployments do not support Aspire-managed container mounts. Move persistent state to a Vercel-supported external service or remove the mount.");
        }

        if (resource.Annotations.OfType<ContainerFilesSourceAnnotation>().Any()
            || resource.Annotations.OfType<ContainerFilesDestinationAnnotation>().Any()
            || resource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire container file mounts, but Vercel Dockerfile deployments upload the source tree and Dockerfile only. Include required files in the source tree or generated Dockerfile output instead.");
        }

        if (resource.Annotations.OfType<ProbeAnnotation>().Any()
            || resource.Annotations.OfType<EndpointProbeAnnotation>().Any()
            || resource.Annotations.OfType<HealthCheckAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire health checks or container probes, but the Vercel preview integration does not map them to Vercel-native checks. Remove the Aspire probes or configure health behavior in Vercel.");
        }

        if (resource.Annotations.OfType<ReplicaAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire replicas or scale, but the Vercel preview integration does not map replica counts to Vercel-native scaling. Configure scaling in Vercel instead.");
        }

        if (resource.Annotations.OfType<WaitAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire wait/dependency ordering, but Vercel deploys each project independently and the preview integration does not preserve Aspire startup ordering. Remove the wait relationship or deploy dependent services separately.");
        }

        ValidateEndpointModel(entry);
        ValidateProjectName(entry);
    }

    private static void ValidateEndpointModel(VercelDeploymentEntry entry)
    {
        var endpoints = entry.Resource.Annotations.OfType<EndpointAnnotation>().ToArray();

        if (endpoints.Length == 0)
        {
            return;
        }

        // Reject the tempting Compose/ACA shapes up front: private listeners, multiple
        // target ports, and non-HTTP protocols do not have an equivalent in this preview's
        // single public Vercel container ingress.
        // Vercel's Dockerfile preview exposes one public platform ingress; it has no
        // Aspire-modeled private service network for internal endpoints.
        var internalEndpoint = endpoints.FirstOrDefault(static endpoint => !endpoint.IsExternal);
        if (internalEndpoint is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures endpoint '{internalEndpoint.Name}' as internal, but Vercel Dockerfile deployments expose public platform HTTPS ingress only. Mark the endpoint external or remove it before deploying to Vercel.");
        }

        var unsupportedEndpoint = endpoints.FirstOrDefault(static endpoint => !IsHttpEndpoint(endpoint));
        if (unsupportedEndpoint is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures endpoint '{unsupportedEndpoint.Name}' with scheme '{unsupportedEndpoint.UriScheme}' and transport '{unsupportedEndpoint.Transport}', but Vercel Dockerfile deployments support only HTTP or HTTPS endpoints with HTTP transports.");
        }

        var targetPorts = endpoints
            .Select(static endpoint => endpoint.TargetPort)
            .Where(static targetPort => targetPort.HasValue)
            .Select(static targetPort => targetPort!.Value)
            .Distinct()
            .ToArray();

        // Vercel provides one runtime listener through $PORT. Additional target ports would
        // look like ACA extra ports, but Vercel has no equivalent modeled here.
        if (targetPorts.Length > 1)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures multiple Aspire endpoint target ports, but Vercel Dockerfile deployments support only one HTTP listening port exposed through the $PORT environment variable.");
        }
    }

    private static void ValidateProjectName(VercelDeploymentEntry entry)
    {
        if (HasVercelProjectLinkFile(entry.SourceRoot))
        {
            return;
        }

        _ = GetVercelProjectName(entry);
    }

    private static async Task<VercelDeploymentEntry> PrepareDeploymentEntryAsync(PipelineStepContext context, VercelDeploymentEntry entry)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string stagingRoot = GetStagingSourceRoot(outputService.GetTempDirectory(entry.Resource), entry);

        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }

        LogSourceUploadWarnings(context.Logger, entry);

        // Vercel deploy uploads the working tree rooted at --cwd. Aspire Dockerfile metadata
        // can point at generated files or Dockerfiles outside that root, so stage a temporary
        // Vercel-shaped source tree instead of mutating the user's checkout.
        bool preserveVercelProjectLink = HasVercelProjectLinkFile(entry.SourceRoot);
        // Staging uses regular file copies. Reject links up front so we do not
        // accidentally dereference outside-root content or change Docker context semantics.
        ValidateNoSymbolicLinks(entry, preserveVercelProjectLink, context.CancellationToken);

        CopyDirectory(
            entry.SourceRoot,
            stagingRoot,
            preserveVercelProjectLink,
            context.CancellationToken);

        await WriteVercelProjectConfigurationAsync(entry.Resource, stagingRoot, context.CancellationToken).ConfigureAwait(false);

        string stagedDockerfilePath = Path.Combine(stagingRoot, VercelDockerfileFileName);
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

    private static async Task WriteVercelProjectConfigurationAsync(
        IResource resource,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        string vercelJsonPath = Path.Combine(stagingRoot, VercelJsonFileName);
        JsonObject root;
        if (File.Exists(vercelJsonPath))
        {
            try
            {
                root = JsonNode.Parse(await File.ReadAllTextAsync(vercelJsonPath, cancellationToken).ConfigureAwait(false)) as JsonObject
                    ?? throw new DistributedApplicationException($"Resource '{resource.Name}' source root contains '{VercelJsonFileName}', but it is not a JSON object.");
            }
            catch (JsonException ex)
            {
                throw new DistributedApplicationException($"Resource '{resource.Name}' source root contains invalid '{VercelJsonFileName}'.", ex);
            }
        }
        else
        {
            root = [];
        }

        if (root.ContainsKey("services"))
        {
            // Do not merge an existing services block. Service names, roots, and rewrites
            // are part of the deployment contract used by endpoint references, so a partial
            // merge could deploy successfully but route traffic to a user-defined service.
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' source root contains '{VercelJsonFileName}' with an existing 'services' configuration. The Vercel preview integration owns the generated container service configuration; remove the existing services block or deploy this project outside the Aspire Vercel integration.");
        }

        var unsupportedKey = VercelJsonServicesModeUnsupportedKeys.FirstOrDefault(root.ContainsKey);
        if (unsupportedKey is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' source root contains '{VercelJsonFileName}' with top-level '{unsupportedKey}', but the Aspire Vercel integration owns the generated services-mode container and catch-all routing configuration. Move that setting into the Dockerfile, AppHost environment variables, or Vercel project settings before deploying with the Aspire Vercel integration.");
        }

        // Dockerfile.vercel is only honored by Vercel's services/container runtime path,
        // so generate the minimal service shape instead of relying on framework detection.
        root["services"] = new JsonObject
        {
            [VercelContainerServiceName] = new JsonObject
            {
                ["runtime"] = "container",
                ["root"] = ".",
                ["entrypoint"] = VercelDockerfileFileName
            }
        };

        JsonObject catchAllRewrite = new()
        {
            ["source"] = "/(.*)",
            ["destination"] = new JsonObject
            {
                ["service"] = VercelContainerServiceName
            }
        };

        // Services mode routes requests to named services explicitly. The generated catch-all
        // rewrite makes Aspire's single Dockerfile-backed workload behave like one public app
        // endpoint instead of relying on Vercel framework routing.
        if (root["rewrites"] is null)
        {
            root["rewrites"] = new JsonArray(catchAllRewrite);
        }
        else if (root["rewrites"] is JsonArray rewrites)
        {
            rewrites.Add(catchAllRewrite);
        }
        else
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' source root contains '{VercelJsonFileName}' with a 'rewrites' value that is not an array.");
        }

        await File.WriteAllTextAsync(vercelJsonPath, root.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private static bool RequiresStaging(VercelDeploymentEntry entry)
    {
        // Publish output should point at the Vercel-facing artifact shape. Generated or
        // non-root Dockerfiles are materialized as Dockerfile.vercel in the staged tree.
        if (entry.Dockerfile.DockerfileFactory is not null)
        {
            return true;
        }

        string dockerfileDirectory = Path.GetDirectoryName(Path.GetFullPath(entry.DockerfilePath)) ?? string.Empty;

        return !PathEquals(dockerfileDirectory, entry.SourceRoot)
            || !string.Equals(Path.GetFileName(entry.DockerfilePath), VercelDockerfileFileName, GetPathStringComparison());
    }

    private static string GetDisplayDockerfilePath(VercelDeploymentEntry entry)
    {
        if (RequiresStaging(entry))
        {
            return VercelDockerfileFileName;
        }

        return Path.GetRelativePath(entry.SourceRoot, entry.DockerfilePath);
    }

    private static string GetStagingSourceRoot(string tempDirectory, VercelDeploymentEntry entry)
    {
        // Use provider identity for the staged folder when possible. Vercel commands use CWD
        // and link metadata, and stable staging names make logs/output easier to correlate
        // without writing anything back to the source checkout.
        string sourceRoot = Path.TrimEndingDirectorySeparator(entry.SourceRoot);
        string sourceRootName = HasVercelProjectLinkFile(entry.SourceRoot)
            ? Path.GetFileName(sourceRoot)
            : GetManagedVercelProjectName(entry);

        if (string.IsNullOrWhiteSpace(sourceRootName))
        {
            sourceRootName = entry.Resource.Name;
        }

        return Path.Combine(tempDirectory, sourceRootName);
    }

    private static void ValidateNoSymbolicLinks(
        VercelDeploymentEntry entry,
        bool preserveVercelProjectLink,
        CancellationToken cancellationToken)
    {
        ValidateNoSymbolicLinks(
            entry.Resource,
            entry.SourceRoot,
            entry.SourceRoot,
            preserveVercelProjectLink,
            cancellationToken);
    }

    private static void ValidateNoSymbolicLinks(
        IResource resource,
        string sourceRoot,
        string directory,
        bool preserveVercelProjectLink,
        CancellationToken cancellationToken)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attributes = File.GetAttributes(path);
            string relativePath = Path.GetRelativePath(sourceRoot, path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new DistributedApplicationException(
                    $"Resource '{resource.Name}' source root contains symbolic link or reparse point '{relativePath}'. Vercel staging copies files into an upload directory and does not preserve symlink semantics. Replace the link with real files inside the source root or adjust the Dockerfile source context.");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                string directoryName = Path.GetFileName(path);
                if (ShouldSkipStagingDirectory(directoryName, preserveVercelProjectLink))
                {
                    continue;
                }

                ValidateNoSymbolicLinks(
                    resource,
                    sourceRoot,
                    path,
                    preserveVercelProjectLink: false,
                    cancellationToken);
            }
        }
    }

    private static void LogSourceUploadWarnings(ILogger logger, VercelDeploymentEntry entry)
    {
        var warningPaths = GetSourceUploadWarningPaths(entry.SourceRoot);
        if (warningPaths.Count == 0)
        {
            return;
        }

        logger.LogWarning(
            "Resource '{ResourceName}' source root contains environment files that may be uploaded to Vercel: {Paths}. Add or update .vercelignore to exclude sensitive content.",
            entry.Resource.Name,
            string.Join(", ", warningPaths));
    }

    internal static IReadOnlyList<string> GetSourceUploadWarningPaths(string sourceRoot)
    {
        var ignorePatterns = ReadVercelIgnorePatterns(sourceRoot);
        List<string> warningPaths = [];

        foreach (string file in Directory.EnumerateFiles(sourceRoot, ".env*"))
        {
            string fileName = Path.GetFileName(file);
            if (IsExampleEnvironmentFile(fileName))
            {
                continue;
            }

            if (!IsIgnoredByVercelIgnore(fileName, isDirectory: false, ignorePatterns))
            {
                warningPaths.Add(fileName);
            }
        }

        return [.. warningPaths.Order(StringComparer.Ordinal)];
    }

    private static bool IsExampleEnvironmentFile(string fileName)
        => fileName.Equals(".env.example", GetPathStringComparison())
            || fileName.Equals(".env.sample", GetPathStringComparison())
            || fileName.Equals(".env.template", GetPathStringComparison());

    private static IReadOnlyList<string> ReadVercelIgnorePatterns(string sourceRoot)
    {
        // This is a warning-only approximation of .vercelignore, not a full gitignore engine.
        // Vercel remains the authority for the actual upload set; this parser only avoids
        // noisy .env warnings for common forms like `.env*`, `secrets/`, `!.env.example`,
        // `nested/path`, and `*.local`.
        string vercelIgnorePath = Path.Combine(sourceRoot, ".vercelignore");
        if (!File.Exists(vercelIgnorePath))
        {
            return [];
        }

        return [.. File.ReadAllLines(vercelIgnorePath)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))];
    }

    private static bool IsIgnoredByVercelIgnore(string relativePath, bool isDirectory, IReadOnlyList<string> ignorePatterns)
    {
        bool ignored = false;
        foreach (string pattern in ignorePatterns)
        {
            bool negated = pattern.StartsWith('!');
            string normalizedPattern = negated ? pattern[1..] : pattern;
            if (string.IsNullOrWhiteSpace(normalizedPattern))
            {
                continue;
            }

            if (VercelIgnorePatternMatches(relativePath, isDirectory, normalizedPattern))
            {
                ignored = !negated;
            }
        }

        return ignored;
    }

    private static bool VercelIgnorePatternMatches(string relativePath, bool isDirectory, string pattern)
    {
        bool directoryOnly = pattern.EndsWith('/');
        if (directoryOnly && !isDirectory)
        {
            return false;
        }

        string normalizedPattern = pattern.Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return false;
        }

        string normalizedPath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        if (normalizedPattern.Contains('/'))
        {
            return WildcardMatches(normalizedPath, normalizedPattern);
        }

        return normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => WildcardMatches(segment, normalizedPattern));
    }

    private static bool WildcardMatches(string value, string pattern)
    {
        string regexPattern = $"^{Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal)}$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory,
        bool preserveVercelProjectLink,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string directoryName = Path.GetFileName(directory);
            if (ShouldSkipStagingDirectory(directoryName, preserveVercelProjectLink))
            {
                continue;
            }

            if (IsVercelDirectory(directoryName))
            {
                // Keep only project identity. Cache/build metadata belongs to the user's
                // checkout and should not be uploaded from Aspire's staged source.
                CopyVercelProjectLink(directory, Path.Combine(destinationDirectory, directoryName), cancellationToken);
                continue;
            }

            CopyDirectory(
                directory,
                Path.Combine(destinationDirectory, directoryName),
                preserveVercelProjectLink: false,
                cancellationToken);
        }

        foreach (string file in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static bool ShouldSkipStagingDirectory(string directoryName, bool preserveVercelProjectLink)
        // These are local checkout/provider cache directories, not app source for Vercel's
        // remote Docker build. Linked projects get only .vercel/project.json via a separate path.
        => IsGitDirectory(directoryName)
            || IsNodeModulesDirectory(directoryName)
            || (IsVercelDirectory(directoryName) && !preserveVercelProjectLink);

    private static void CopyVercelProjectLink(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string projectJsonPath = Path.Combine(sourceDirectory, "project.json");
        if (!File.Exists(projectJsonPath))
        {
            return;
        }

        Directory.CreateDirectory(destinationDirectory);
        File.Copy(projectJsonPath, Path.Combine(destinationDirectory, "project.json"), overwrite: true);
    }

    private static bool IsGitDirectory(string directoryName)
        => string.Equals(directoryName, ".git", GetPathStringComparison());

    private static bool IsNodeModulesDirectory(string directoryName)
        => string.Equals(directoryName, "node_modules", GetPathStringComparison());

    private static bool IsVercelDirectory(string directoryName)
        => string.Equals(directoryName, ".vercel", GetPathStringComparison());

    private static bool PathEquals(string path, string otherPath)
        => string.Equals(Path.GetFullPath(path), Path.GetFullPath(otherPath), GetPathStringComparison());

    private static StringComparison GetPathStringComparison()
        => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    internal static string GetVercelProjectName(VercelDeploymentEntry entry)
        => GetVercelProjectLink(entry).ProjectName;

    internal static string GetVercelProjectName(IResource resource)
    {
        // This integration does not synthesize Dockerfiles from arbitrary compute resources.
        // The workload must already carry Aspire Dockerfile publish metadata so Vercel receives
        // the same source/Dockerfile contract the user opted into.
        if (!resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile))
        {
            throw new DistributedApplicationException($"Resource '{resource.Name}' targets Vercel but does not have Aspire Dockerfile build metadata. Use a workload integration that publishes Dockerfile metadata, call PublishAsDockerFile, or configure the resource with WithDockerfile, WithDockerfileFactory, or WithDockerfileBuilder.");
        }

        return GetVercelProjectName(new VercelDeploymentEntry(resource, dockerfile.ContextPath, dockerfile.DockerfilePath, dockerfile));
    }

    private static VercelProjectLink GetVercelProjectLink(VercelDeploymentEntry entry)
    {
        if (TryReadVercelProjectLink(entry.SourceRoot, out var projectLink))
        {
            return projectLink;
        }

        return new(GetManagedVercelProjectName(entry), ProjectId: null);
    }

    private static string GetManagedVercelProjectName(VercelDeploymentEntry entry)
    {
        if (entry.Resource.TryGetLastAnnotation<VercelProjectOptionsAnnotation>(out var options))
        {
            return options.ProjectName;
        }

        // The production endpoint contract is project-name based, so managed names must
        // be stable and Vercel-valid before deploy starts.
        string sourceRoot = Path.TrimEndingDirectorySeparator(entry.SourceRoot);
        string sourceRootName = Path.GetFileName(sourceRoot);

        if (TryCreateVercelProjectName(sourceRootName, out string? projectName)
            || TryCreateVercelProjectName(entry.Resource.Name, out projectName))
        {
            return projectName;
        }

        throw new DistributedApplicationException($"Could not infer a valid Vercel project name for resource '{entry.Resource.Name}' from source root '{entry.SourceRoot}'. Rename the source directory or link the source root to an existing Vercel project.");
    }

    internal static bool IsValidVercelProjectName(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName)
            || projectName.Length > VercelProjectNameMaxLength
            || !IsLowercaseAsciiLetterOrDigit(projectName[0])
            || !IsLowercaseAsciiLetterOrDigit(projectName[^1]))
        {
            return false;
        }

        return projectName.All(static character =>
            IsLowercaseAsciiLetterOrDigit(character)
            || character == '-');
    }

    private static bool TryCreateVercelProjectName(string? value, [NotNullWhen(true)] out string? projectName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            projectName = null;
            return false;
        }

        var builder = new StringBuilder(value.Length);
        bool previousWasSeparator = false;

        foreach (char character in value)
        {
            if (IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        projectName = builder
            .ToString()
            .Trim('-');

        if (projectName.Length > VercelProjectNameMaxLength)
        {
            projectName = projectName[..VercelProjectNameMaxLength].Trim('-');
        }

        if (projectName.Length == 0)
        {
            projectName = null;
            return false;
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(char character)
        => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool IsLowercaseAsciiLetterOrDigit(char character)
        => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool HasVercelProjectLinkFile(string sourceRoot)
        => File.Exists(GetVercelProjectJsonPath(sourceRoot));

    private static bool TryReadVercelProjectLink(string sourceRoot, [NotNullWhen(true)] out VercelProjectLink? projectLink)
    {
        string projectJsonPath = GetVercelProjectJsonPath(sourceRoot);

        if (File.Exists(projectJsonPath))
        {
            // Vercel CLI writes linked project identity as:
            //   .vercel/project.json: { "projectId": "...", "projectName": "..." }
            // Treat it as provider ownership metadata rather than regenerating a managed name.
            using var document = JsonDocument.Parse(File.ReadAllText(projectJsonPath));
            string? projectName = GetJsonStringProperty(document.RootElement, "projectName");

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                projectLink = new(projectName, GetJsonStringProperty(document.RootElement, "projectId"));
                return true;
            }
        }

        projectLink = null;
        return false;
    }

    private static string? GetJsonStringProperty(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString())
                ? property.GetString()
                : null;

    private static string GetVercelProjectJsonPath(string sourceRoot)
        => Path.Combine(sourceRoot, ".vercel", "project.json");

    internal static string GetDeploymentUrl(string standardOutput)
        => GetDeploymentResult(standardOutput).DeploymentUrl;

    internal static VercelDeploymentResult GetDeploymentResult(string standardOutput)
    {
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

internal sealed record VercelDeploymentEntry(IResource Resource, string SourceRoot, string DockerfilePath, DockerfileBuildAnnotation Dockerfile);

internal sealed record VercelDeploymentPlan(string Environment, VercelDeploymentPlanEntry[] Deployments);

internal sealed record VercelDeploymentPlanEntry(string ResourceName, string DockerfilePath, string DeployCommand, string[] EnvironmentVariables);

internal sealed record VercelEnvironmentConfiguration(
    IReadOnlyList<KeyValuePair<string, string>> DeploymentEnvironmentVariables,
    IReadOnlyList<KeyValuePair<string, string>> ProjectEnvironmentVariables)
{
    public static VercelEnvironmentConfiguration Empty { get; } = new([], []);

    public IEnumerable<string> AllEnvironmentVariableNames =>
        DeploymentEnvironmentVariables.Select(static variable => variable.Key)
            .Concat(ProjectEnvironmentVariables.Select(static variable => variable.Key));
}

internal sealed record VercelDeploymentResult(string? DeploymentId, string DeploymentUrl);

internal sealed record VercelDeploymentInspection(string? ReadyState);

internal sealed record VercelDeploymentState(
    int SchemaVersion,
    string Environment,
    string? Scope,
    string? Target,
    bool Production,
    VercelDeploymentStateEntry[] Deployments);

internal sealed record VercelDeploymentStateEntry(
    string ResourceName,
    string ProjectName,
    string? ProjectId,
    string? DeploymentId,
    string? DeploymentUrl,
    string SourceRoot,
    bool ManagedByAspire)
{
    public string? ProductionUrl { get; init; }
}

internal sealed record VercelProjectLink(string ProjectName, string? ProjectId);
