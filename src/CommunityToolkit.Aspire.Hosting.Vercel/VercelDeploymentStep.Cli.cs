#pragma warning disable ASPIREPIPELINES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Vercel deployment-step helpers for CLI prerequisite validation, deployment verification,
/// and shared error shaping around external tool failures.
/// </summary>
internal static partial class VercelDeploymentStep
{
    public static async Task ValidateCliPrerequisitesAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();

        var versionResult = await runner.GetVersionAsync(context.CancellationToken).ConfigureAwait(false);
        if (!versionResult.Succeeded)
        {
            throw CreateCliException("validate Vercel CLI installation", VercelCliFileName, versionResult);
        }

        var versionOutput = $"{versionResult.StandardOutput}{Environment.NewLine}{versionResult.StandardError}";
        if (!VercelCliOutputParser.TryGetCliVersion(versionOutput, out var version))
        {
            throw new DistributedApplicationException(
                $"Failed to determine Vercel CLI version from '{VercelCliOutputParser.GetTrimmedOutput(versionOutput)}'. Install Vercel CLI {MinimumVercelCliVersion} or later from https://vercel.com/docs/cli.");
        }

        // The preview relies on newer CLI behavior: project-scoped link/pull,
        // prebuilt deploys, deployment-scoped --env, JSON inspect output with
        // --wait/--timeout, and project removal.
        if (version < MinimumVercelCliVersion)
        {
            throw new DistributedApplicationException(
                $"Vercel CLI version '{version}' is not supported. Install Vercel CLI {MinimumVercelCliVersion} or later from https://vercel.com/docs/cli.");
        }

        var whoamiResult = await runner.GetCurrentUserAsync(context.CancellationToken).ConfigureAwait(false);
        if (!whoamiResult.Succeeded)
        {
            throw CreateCliException("validate Vercel authentication", VercelCliFileName, whoamiResult);
        }

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            var scopeResult = await runner.ListProjectsAsync(options, filter: null, context.CancellationToken).ConfigureAwait(false);
            if (!scopeResult.Succeeded)
            {
                throw CreateCliException($"validate Vercel scope '{options.Scope}'", VercelCliFileName, scopeResult);
            }
        }
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
        var result = await runner.InspectDeploymentAsync(options, deploymentResult.DeploymentUrl, context.CancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw CreateCliException($"verify Vercel deployment for resource '{resourceName}'", VercelCliFileName, result);
        }

        var inspection = VercelCliOutputParser.GetDeploymentInspection(result.StandardOutput);
        if (inspection.ReadyState is null)
        {
            throw new DistributedApplicationException($"Vercel inspect output for resource '{resourceName}' did not include a deployment ready state. Output: {VercelCliOutputParser.GetTrimmedOutput(result.StandardOutput)}");
        }

        if (!string.Equals(inspection.ReadyState, "READY", StringComparison.OrdinalIgnoreCase))
        {
            throw new DistributedApplicationException($"Vercel deployment for resource '{resourceName}' finished with state '{inspection.ReadyState}' instead of 'READY'.");
        }
    }

    private static DistributedApplicationException CreateCliException(string operation, string cliPath, VercelCliResult result)
    {
        string output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return new DistributedApplicationException($"Failed to {operation} using '{cliPath}' (exit code {result.ExitCode}). {output}");
    }
}
