#pragma warning disable ASPIREPIPELINES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelDeploymentPlanWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<string> WriteAsync(
        DistributedApplicationModel model,
        VercelEnvironmentResource environment,
        string outputDirectory,
        CancellationToken cancellationToken)
        => await WriteAsync(
            executionContext: null,
            logger: null,
            model,
            environment,
            outputDirectory,
            cancellationToken).ConfigureAwait(false);

    public static async Task<string> WriteAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        DistributedApplicationModel model,
        VercelEnvironmentResource environment,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToList();
        VercelDeploymentModel.ValidateEntries(entries);

        // Publish is a reviewable handoff, not a dry-run deploy. Keep it deterministic:
        // show commands, Dockerfile paths, and env var names without resolving secrets or
        // depending on mutable Vercel state.
        Directory.CreateDirectory(outputDirectory);
        var options = environment.GetVercelOptions();

        var plan = new VercelDeploymentPlan(
            environment.Name,
            await CreateEntriesAsync(
                executionContext,
                logger,
                options,
                entries,
                cancellationToken).ConfigureAwait(false));

        string planPath = Path.Combine(outputDirectory, VercelDeploymentStep.DeploymentPlanFileName);
        await using FileStream stream = File.Create(planPath);
        await JsonSerializer.SerializeAsync(stream, plan, JsonOptions, cancellationToken).ConfigureAwait(false);

        return planPath;
    }

    public static async Task<string[]> BuildDeployArgumentsAsync(
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

    public static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        // Publish plans must be useful without Vercel credentials or secret resolution.
        // Secret-bearing values are reduced to names/placeholders here; deploy resolves them
        // only when they are sent to Vercel's project env store over stdin.
        var entriesByResourceName = VercelDeploymentModel.GetEntriesByResourceName(entries);
        var environmentConfiguration = await GetEnvironmentConfigurationAsync(
            executionContext,
            logger,
            options,
            entry,
            entriesByResourceName,
            resolveProjectEnvironmentVariableValues: false,
            cancellationToken).ConfigureAwait(false);

        return VercelCliArguments.BuildDeployArguments(options, VercelDeploymentPaths.GetDeployDirectory(entry), VercelProjectNameResolver.GetProjectOption(entry), environmentConfiguration.DeploymentEnvironmentVariables);
    }

    public static async Task<VercelEnvironmentConfiguration> GetEnvironmentConfigurationAsync(
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

        VercelEnvironmentMapper.ValidateUnsupportedRuntimeConfiguration(entry.Resource, executionConfiguration);

        var environmentVariables = await VercelEnvironmentMapper.GetConfigurationAsync(
            entry.Resource,
            options,
            executionConfiguration,
            entriesByResourceName,
            resolveProjectEnvironmentVariableValues,
            cancellationToken).ConfigureAwait(false);

        return environmentVariables;
    }

    private static async Task<VercelDeploymentPlanEntry[]> CreateEntriesAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        List<VercelDeploymentPlanEntry> planEntries = [];
        var entriesByResourceName = VercelDeploymentModel.GetEntriesByResourceName(entries);

        foreach (var entry in entries)
        {
            // When execution context is available, include the same target-native env names
            // deploy will use. Values stay redacted because publish output is committed or
            // handed off more often than deploy logs.
            var environmentConfiguration = executionContext is null || logger is null
                ? VercelEnvironmentConfiguration.Empty
                : await GetEnvironmentConfigurationAsync(executionContext, logger, options, entry, entriesByResourceName, resolveProjectEnvironmentVariableValues: false, cancellationToken).ConfigureAwait(false);

            planEntries.Add(new(
                entry.Resource.Name,
                VercelDeploymentModel.GetDisplayDockerfilePath(entry),
                VercelCliArguments.BuildDisplayDeployCommand(options, entry.Resource.Name, environmentConfiguration.DeploymentEnvironmentVariables),
                [.. environmentConfiguration.AllEnvironmentVariableNames.Order(StringComparer.Ordinal)]));
        }

        return [.. planEntries];
    }
}
