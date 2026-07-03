#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal sealed record VercelDeploymentEntry(
    IResource Resource,
    string SourceRoot,
    string? DockerfilePath = null,
    DockerfileBuildAnnotation? Dockerfile = null,
    string TempDirectory = "",
    string DeployDirectory = "")
{
    public string EffectiveDeployDirectory => string.IsNullOrWhiteSpace(DeployDirectory) ? SourceRoot : DeployDirectory;
}

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

internal sealed record PreviousVercelDeployment(VercelDeploymentStateEntry Entry, string ProjectEnvironment);

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

    public string? VcrImageDigest { get; init; }

    public int? BuildOutputApiVersion { get; init; }

    public string[] ProjectEnvironmentVariables { get; init; } = [];
}

internal sealed record VercelImageReference(string Reference, string Digest);

internal sealed record VercelPreparedDeploymentAnnotation(
    VercelDeploymentEntry Entry,
    VercelProjectLink ProjectLink,
    VercelPulledProjectContext ProjectContext,
    bool ManagedByAspire,
    string RemoteImageName,
    string RemoteImageTag,
    string TaggedImageReference) : IResourceAnnotation;

internal sealed class VercelImagePushOptionsCallbackAnnotation : IResourceAnnotation
{
}

internal sealed record VercelProjectLink(string ProjectName, string? ProjectId);

internal sealed record VercelPulledProject(
    string ProjectName,
    string? ProjectId,
    string? OrgId,
    string ProjectJsonContent,
    string OidcToken);

internal sealed record VercelPulledProjectContext(
    VercelEnvironmentConfiguration EnvironmentConfiguration,
    VercelPulledProject PulledProject,
    VercelOidcClaims OidcClaims);

internal sealed record VercelPulledProjectSettings(string ProjectName, string? ProjectId, string? OrgId);

internal sealed record VercelOidcClaims(string? OwnerId, string? Owner, string? Project, string? ProjectId);
