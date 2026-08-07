#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Vercel deployment-step helpers that bridge Aspire pipeline callbacks to the testable
/// publish-plan writer.
/// </summary>
internal static partial class VercelDeploymentStep
{
    public static async Task WriteDeploymentPlanAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string outputDirectory = outputService.GetOutputDirectory(environment);

        string planPath = await VercelDeploymentPlanWriter.WriteAsync(
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
        => await VercelDeploymentPlanWriter.WriteAsync(model, environment, outputDirectory, cancellationToken).ConfigureAwait(false);

    internal static async Task<string> WriteDeploymentPlanAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        DistributedApplicationModel model,
        VercelEnvironmentResource environment,
        string outputDirectory,
        CancellationToken cancellationToken)
        => await VercelDeploymentPlanWriter.WriteAsync(executionContext, logger, model, environment, outputDirectory, cancellationToken).ConfigureAwait(false);

    internal static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        CancellationToken cancellationToken)
        => await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(executionContext, logger, options, entry, cancellationToken).ConfigureAwait(false);

    internal static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
        => await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(executionContext, logger, options, entry, entries, cancellationToken).ConfigureAwait(false);
}
