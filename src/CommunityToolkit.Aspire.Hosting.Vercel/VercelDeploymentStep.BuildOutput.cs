#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using Aspire.Hosting;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Vercel deployment-step helpers for preparing linked project directories, pulling provider
/// metadata, and writing Build Output API files before invoking <c>vercel deploy --prebuilt</c>.
/// </summary>
internal static partial class VercelDeploymentStep
{
    private static async Task<string> PrepareProjectEnvironmentDirectoryAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string projectLinkDirectory = Path.Combine(outputService.GetTempDirectory(entry.Resource), ".vercel-project");
        if (Directory.Exists(projectLinkDirectory))
        {
            Directory.Delete(projectLinkDirectory, recursive: true);
        }
        Directory.CreateDirectory(projectLinkDirectory);

        // `vercel env add` is project-scoped but intentionally does not accept --project.
        // See https://vercel.com/docs/cli/env and https://vercel.com/docs/cli/link.
        // Link a scratch directory instead of the source root so secret configuration can use
        // the CLI's native project lookup without writing .vercel metadata into user code.
        string[] linkArguments = VercelCliArguments.BuildLinkProjectArguments(options, projectLinkDirectory, VercelProjectNameResolver.GetProjectOption(entry));
        var result = await runner.RunAsync(VercelCliFileName, linkArguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"prepare temporary Vercel project link for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }

        return projectLinkDirectory;
    }

    private static async Task<VercelPulledProject> PullProjectSettingsAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        string projectLinkDirectory)
    {
        // `vercel pull` is the documented way to materialize project settings and environment
        // files under `.vercel/`; VCR authentication depends on the pulled VERCEL_OIDC_TOKEN.
        // See https://vercel.com/docs/cli/pull and https://vercel.com/docs/container-registry.
        string targetEnvironment = VercelProjectEnvironment.GetName(options);
        string[] arguments = VercelCliArguments.BuildPullProjectSettingsArguments(options, projectLinkDirectory, targetEnvironment);
        var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"pull Vercel project settings for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }

        string vercelDirectory = Path.Combine(projectLinkDirectory, VercelConstants.DirectoryName);
        string projectJsonPath = Path.Combine(vercelDirectory, VercelConstants.ProjectFileName);
        string environmentPath = Path.Combine(vercelDirectory, $".env.{targetEnvironment}.local");

        if (!File.Exists(projectJsonPath))
        {
            throw new DistributedApplicationException($"Vercel pull did not write expected project settings file '{projectJsonPath}' for resource '{entry.Resource.Name}'.");
        }

        if (!File.Exists(environmentPath))
        {
            throw new DistributedApplicationException($"Vercel pull did not write expected environment file '{environmentPath}' for resource '{entry.Resource.Name}'.");
        }

        var environmentVariables = VercelDotEnvParser.Parse(await File.ReadAllLinesAsync(environmentPath, context.CancellationToken).ConfigureAwait(false));
        if (!environmentVariables.TryGetValue(VercelConstants.OidcTokenEnvironmentVariable, out string? oidcToken)
            || string.IsNullOrWhiteSpace(oidcToken))
        {
            throw new DistributedApplicationException($"Vercel pull did not provide {VercelConstants.OidcTokenEnvironmentVariable}, which is required to authenticate local Docker builds to VCR.");
        }

        string projectJsonContent = await File.ReadAllTextAsync(projectJsonPath, context.CancellationToken).ConfigureAwait(false);
        var project = VercelProjectSettingsReader.Read(projectJsonPath, projectJsonContent);

        // `vercel pull` materializes project secrets next to the scratch link so local
        // builders can read them. This integration only needs the short-lived OIDC token
        // and project metadata; delete the env files before creating deploy artifacts.
        if (File.Exists(environmentPath))
        {
            File.Delete(environmentPath);
        }

        string localEnvironmentPath = Path.Combine(projectLinkDirectory, ".env.local");
        if (File.Exists(localEnvironmentPath))
        {
            File.Delete(localEnvironmentPath);
        }

        return new(project.ProjectName, project.ProjectId, project.OrgId, projectJsonContent, oidcToken);
    }

    private static async Task LoginToVcrAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        string oidcToken,
        VercelOidcClaims claims)
        => await LoginToVcrAsync(runner, oidcToken, claims, context.CancellationToken).ConfigureAwait(false);

    internal static async Task LoginToVcrAsync(
        IVercelCliRunner runner,
        string oidcToken,
        VercelOidcClaims claims,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claims.OwnerId))
        {
            throw new DistributedApplicationException("The Vercel OIDC token did not include the owner_id claim required to authenticate to VCR.");
        }

        // VCR supports Docker-compatible tooling at vcr.vercel.com. This login uses the
        // Vercel-issued OIDC token pulled for the linked project.
        // See https://vercel.com/docs/container-registry.
        string[] arguments = VercelCliArguments.BuildDockerLoginArguments(claims.OwnerId);
        var result = await runner.RunAsync(DockerCliFileName, arguments, workingDirectory: null, cancellationToken, standardInput: oidcToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException("authenticate Docker to VCR", DockerCliFileName, result);
        }
    }

    private static async Task EnsureManagedProjectAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry)
    {
        // `vercel project add` is idempotent for the current login/scope in the CLI versions
        // this integration supports: it creates the project or validates that it already
        // exists and is accessible. Failure here means deploy should not proceed to image push.
        string projectName = VercelProjectNameResolver.GetProjectName(entry);
        string[] arguments = VercelCliArguments.BuildAddProjectArguments(options, projectName);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"create or validate Vercel project '{projectName}' for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }
    }
}
