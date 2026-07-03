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
        // Publish plans must be useful without Vercel credentials or secret resolution.
        // Secret-bearing values are reduced to names/placeholders here; deploy resolves them
        // only when they are sent to Vercel's project env store over stdin.
        var entriesByResourceName = GetDeploymentEntriesByResourceName(entries);
        var environmentConfiguration = await GetVercelEnvironmentConfigurationAsync(
            executionContext,
            logger,
            options,
            entry,
            entriesByResourceName,
            resolveProjectEnvironmentVariableValues: false,
            cancellationToken).ConfigureAwait(false);

        return BuildDeployArguments(options, GetDeployDirectory(entry), GetVercelProjectOption(entry), environmentConfiguration.DeploymentEnvironmentVariables);
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

}
