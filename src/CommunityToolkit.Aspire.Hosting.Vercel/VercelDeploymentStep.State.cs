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
        var linkedDeploymentsWithEnvironmentVariables = state.Deployments
            .Where(static deployment => !deployment.ManagedByAspire && deployment.ProjectEnvironmentVariables.Length > 0)
            .OrderBy(static deployment => deployment.ProjectName, StringComparer.Ordinal)
            .ThenBy(static deployment => deployment.ResourceName, StringComparer.Ordinal)
            .ToArray();

        if (projects.Length == 0 && linkedDeploymentsWithEnvironmentVariables.Length == 0)
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

        foreach (var deployment in linkedDeploymentsWithEnvironmentVariables)
        {
            await RemoveLinkedProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                environment,
                deployment,
                GetVercelProjectEnvironmentName(state)).ConfigureAwait(false);
        }

        if (projects.Length == 0)
        {
            context.Summary.Add("Vercel destroy", $"No Aspire-managed Vercel deployments were found for environment '{environment.Name}'.");
            await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (string projectName in projects)
        {
            if (!await ProjectExistsAsync(context, runner, options, projectName).ConfigureAwait(false))
            {
                context.Summary.Add("Vercel project already absent", projectName);
            }
            else
            {
                string[] arguments = BuildDestroyProjectArguments(options, projectName);
                var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken, standardInput: "y\n").ConfigureAwait(false);

                if (!result.Succeeded)
                {
                    if (await ProjectExistsAsync(context, runner, options, projectName).ConfigureAwait(false))
                    {
                        throw CreateCliException($"destroy Vercel project '{projectName}'", VercelCliFileName, result);
                    }

                    context.Summary.Add("Vercel project already absent", projectName);
                }
                else
                {
                    context.Summary.Add("Vercel project removed", projectName);
                }
            }

            state = RemoveManagedProjectFromDeploymentState(state, projectName);
            // Save after each project removal so a later CLI failure leaves retryable state
            // for projects that still exist instead of forgetting partially cleaned resources.
            stateSection.SetValue(JsonSerializer.Serialize(state, JsonOptions));
            await stateManager.SaveSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
        }

        await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
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

    private static async Task<PreviousVercelDeployment?> GetPreviousDeploymentStateEntryAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        string projectName)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = ReadDeploymentState(stateSection);
        if (existingState is null)
        {
            return null;
        }

        ValidateDeploymentState(environment, options, existingState);
        var entry = existingState.Deployments.FirstOrDefault(deployment =>
            string.Equals(deployment.ResourceName, resourceName, StringComparison.Ordinal)
            && string.Equals(deployment.ProjectName, projectName, StringComparison.Ordinal));

        return entry is null
            ? null
            : new(entry, GetVercelProjectEnvironmentName(existingState));
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

    private static string GetDeployDirectory(VercelDeploymentEntry entry)
        => string.IsNullOrWhiteSpace(entry.DeployDirectory) ? entry.SourceRoot : entry.DeployDirectory;

    private static VercelDeploymentState RemoveManagedProjectFromDeploymentState(VercelDeploymentState state, string projectName)
        => state with
        {
            Deployments = state.Deployments
                .Where(deployment => !deployment.ManagedByAspire || !string.Equals(deployment.ProjectName, projectName, StringComparison.Ordinal))
                .ToArray()
        };

    private static string GetStateSectionName(VercelEnvironmentResource environment) => $"{StateSectionNamePrefix}{environment.Name}";

    private static async Task<bool> ProjectExistsAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string projectName)
    {
        // Avoid treating localized or reformatted CLI errors as provider state. The Vercel
        // CLI exposes project lists as JSON, so destroy checks exact project names before
        // deleting and again after a failed delete to distinguish races from real failures.
        string[] arguments = BuildListProjectsArguments(options, projectName);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"list Vercel projects while checking for '{projectName}'", VercelCliFileName, result);
        }

        return ProjectListContainsProject(result.StandardOutput, projectName);
    }

    private static async Task<bool> ProjectEnvironmentVariableExistsAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
    {
        // `vercel env rm` reports absence as human text, but `vercel env ls --format=json`
        // returns the linked project's exact keys. Use that provider read for idempotent
        // stale-secret cleanup instead of parsing failure prose.
        string[] arguments = BuildListProjectEnvironmentVariablesArguments(options, projectLinkDirectory, targetEnvironment);
        var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"list Vercel project environment variables before removing '{name}'", VercelCliFileName, result);
        }

        return EnvironmentVariableListContainsName(result.StandardOutput, name);
    }

    private static async Task ConfigureProjectEnvironmentVariablesAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        string projectLinkDirectory,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables,
        PreviousVercelDeployment? previousDeployment)
    {
        var currentNames = environmentVariables
            .Select(static variable => variable.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (previousDeployment is not null)
        {
            string[] staleNames = previousDeployment.Entry.ProjectEnvironmentVariables
                .Where(name => !currentNames.Contains(name))
                .Order(StringComparer.Ordinal)
                .ToArray();

            await RemoveProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                projectLinkDirectory,
                entry.Resource.Name,
                staleNames,
                previousDeployment.ProjectEnvironment).ConfigureAwait(false);
        }

        if (environmentVariables.Count == 0)
        {
            return;
        }

        string targetEnvironment = GetVercelProjectEnvironmentName(options);
        foreach (var environmentVariable in environmentVariables.OrderBy(static variable => variable.Key, StringComparer.Ordinal))
        {
            string[] arguments = BuildAddProjectEnvironmentVariableArguments(options, projectLinkDirectory, environmentVariable.Key, targetEnvironment);
            var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken, standardInput: environmentVariable.Value).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw CreateCliException($"configure Vercel project environment variable '{environmentVariable.Key}' for resource '{entry.Resource.Name}'", VercelCliFileName, result);
            }
        }
    }

    private static async Task RemoveProjectEnvironmentVariablesAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string resourceName,
        IReadOnlyList<string> names,
        string targetEnvironment)
    {
        foreach (string name in names)
        {
            if (!await ProjectEnvironmentVariableExistsAsync(context, runner, options, projectLinkDirectory, name, targetEnvironment).ConfigureAwait(false))
            {
                continue;
            }

            string[] arguments = BuildRemoveProjectEnvironmentVariableArguments(options, projectLinkDirectory, name, targetEnvironment);
            var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
            if (!result.Succeeded
                && await ProjectEnvironmentVariableExistsAsync(context, runner, options, projectLinkDirectory, name, targetEnvironment).ConfigureAwait(false))
            {
                throw CreateCliException($"remove stale Vercel project environment variable '{name}' for resource '{resourceName}'", VercelCliFileName, result);
            }
        }
    }

    private static async Task RemoveLinkedProjectEnvironmentVariablesAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelEnvironmentResource environment,
        VercelDeploymentStateEntry deployment,
        string targetEnvironment)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string projectLinkDirectory = Path.Combine(outputService.GetTempDirectory(environment), ".vercel-projects", deployment.ProjectName);
        DeleteDirectoryIfExists(projectLinkDirectory);
        Directory.CreateDirectory(projectLinkDirectory);

        try
        {
            string[] linkArguments = BuildLinkProjectArguments(options, projectLinkDirectory, deployment.ProjectId ?? deployment.ProjectName);
            var linkResult = await runner.RunAsync(VercelCliFileName, linkArguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
            if (!linkResult.Succeeded)
            {
                throw CreateCliException($"link Vercel project '{deployment.ProjectName}' for environment variable cleanup", VercelCliFileName, linkResult);
            }

            await RemoveProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                projectLinkDirectory,
                deployment.ResourceName,
                deployment.ProjectEnvironmentVariables,
                targetEnvironment).ConfigureAwait(false);
        }
        finally
        {
            DeleteDirectoryIfExists(projectLinkDirectory);
        }
    }
}
