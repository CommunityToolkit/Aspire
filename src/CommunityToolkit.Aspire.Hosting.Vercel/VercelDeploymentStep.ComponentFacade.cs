#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using System.Diagnostics.CodeAnalysis;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static partial class VercelDeploymentStep
{
    internal static IEnumerable<VercelDeploymentEntry> GetDeploymentEntries(DistributedApplicationModel model, VercelEnvironmentResource environment)
        => VercelDeploymentModel.GetEntries(model, environment);

    internal static string[] BuildDeployArguments(VercelEnvironmentOptionsAnnotation options, VercelDeploymentEntry entry)
        => VercelCliArguments.BuildDeployArguments(options, entry);

    internal static string[] BuildDockerInspectDigestArguments(string imageReference)
        => VercelCliArguments.BuildDockerInspectDigestArguments(imageReference);

    internal static string[] BuildDestroyProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
        => VercelCliArguments.BuildDestroyProjectArguments(options, projectName);

    internal static string[] BuildListProjectEnvironmentVariablesArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string targetEnvironment)
        => VercelCliArguments.BuildListProjectEnvironmentVariablesArguments(options, projectLinkDirectory, targetEnvironment);

    internal static string[] BuildAddProjectEnvironmentVariableArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
        => VercelCliArguments.BuildAddProjectEnvironmentVariableArguments(options, projectLinkDirectory, name, targetEnvironment);

    internal static string[] BuildRemoveProjectEnvironmentVariableArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
        => VercelCliArguments.BuildRemoveProjectEnvironmentVariableArguments(options, projectLinkDirectory, name, targetEnvironment);

    internal static string[] BuildLinkProjectArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string projectNameOrId)
        => VercelCliArguments.BuildLinkProjectArguments(options, projectLinkDirectory, projectNameOrId);

    internal static string[] BuildValidateScopeArguments(VercelEnvironmentOptionsAnnotation options)
        => VercelCliArguments.BuildValidateScopeArguments(options);

    internal static string[] BuildListProjectsArguments(VercelEnvironmentOptionsAnnotation options, string? filter = null)
        => VercelCliArguments.BuildListProjectsArguments(options, filter);

    internal static string[] BuildInspectDeploymentArguments(VercelEnvironmentOptionsAnnotation options, string deploymentUrl)
        => VercelCliArguments.BuildInspectDeploymentArguments(options, deploymentUrl);

    internal static string GetDockerImageDigest(string output)
        => VercelDockerImageDigestParser.GetDigest(output);

    internal static VercelOidcClaims DecodeUnvalidatedOidcClaims(string token)
        => VercelOidcToken.DecodeUnvalidatedClaims(token);

    internal static Dictionary<string, string> ParseDotEnvFile(IEnumerable<string> lines)
        => VercelDotEnvParser.Parse(lines);

    internal static bool ProjectListContainsProject(string standardOutput, string projectName)
        => VercelCliOutputParser.ProjectListContainsProject(standardOutput, projectName);

    internal static bool EnvironmentVariableListContainsName(string standardOutput, string name)
        => VercelCliOutputParser.EnvironmentVariableListContainsName(standardOutput, name);

    internal static string GetDeploymentUrl(string standardOutput)
        => VercelCliOutputParser.GetDeploymentUrl(standardOutput);

    internal static VercelDeploymentResult GetDeploymentResult(string standardOutput)
        => VercelCliOutputParser.GetDeploymentResult(standardOutput);

    internal static VercelDeploymentInspection GetDeploymentInspection(string standardOutput)
        => VercelCliOutputParser.GetDeploymentInspection(standardOutput);

    internal static bool TryGetVercelCliVersion(string output, [NotNullWhen(true)] out Version? version)
        => VercelCliOutputParser.TryGetCliVersion(output, out version);

    internal static string GetVercelProjectName(VercelDeploymentEntry entry)
        => VercelProjectNameResolver.GetProjectName(entry);

    internal static string GetVercelProjectName(IResource resource)
        => VercelProjectNameResolver.GetProjectName(resource);

    internal static bool IsValidVercelProjectName(string projectName)
        => VercelProjectNameResolver.IsValidProjectName(projectName);

    internal static Task WriteBuildOutputAsync(
        VercelDeploymentEntry entry,
        VercelPulledProject project,
        string imageReference,
        CancellationToken cancellationToken)
        => VercelBuildOutputWriter.WriteAsync(entry, project, imageReference, cancellationToken);
}
