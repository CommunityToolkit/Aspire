#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Captures the Aspire workload and source/build metadata needed to prepare one Vercel project
/// without repeatedly walking the app model.
/// </summary>
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

/// <summary>
/// Serializable publish output that lets users review which workloads would be deployed to Vercel.
/// </summary>
internal sealed record VercelDeploymentPlan(string Environment, VercelDeploymentPlanEntry[] Deployments);

/// <summary>
/// One workload row in the publish plan, including the Dockerfile source and deploy command shape.
/// </summary>
internal sealed record VercelDeploymentPlanEntry(string ResourceName, string DockerfilePath, string DeployCommand, string[] EnvironmentVariables);

/// <summary>
/// Splits Aspire environment values into Vercel deployment arguments and project environment
/// variables because secrets must be configured through provider storage instead of CLI args.
/// </summary>
internal sealed record VercelEnvironmentConfiguration(
    IReadOnlyList<KeyValuePair<string, string>> DeploymentEnvironmentVariables,
    IReadOnlyList<KeyValuePair<string, string>> ProjectEnvironmentVariables)
{
    public static VercelEnvironmentConfiguration Empty { get; } = new([], []);

    public IEnumerable<string> AllEnvironmentVariableNames =>
        DeploymentEnvironmentVariables.Select(static variable => variable.Key)
            .Concat(ProjectEnvironmentVariables.Select(static variable => variable.Key));
}

/// <summary>
/// Typed result from <c>vercel deploy</c>; the URL is used for verification and the optional ID
/// is persisted when the CLI provides it.
/// </summary>
internal sealed record VercelDeploymentResult(string? DeploymentId, string DeploymentUrl);

/// <summary>
/// Typed result from <c>vercel inspect</c> containing only the provider readiness state deploy needs.
/// </summary>
internal sealed record VercelDeploymentInspection(string? ReadyState);

/// <summary>
/// Combines a persisted deployment entry with the Vercel environment name that originally
/// configured it, so redeploy and cleanup target the same provider scope.
/// </summary>
internal sealed record PreviousVercelDeployment(VercelDeploymentStateEntry Entry, string ProjectEnvironment);

/// <summary>
/// Persisted ownership record for a Vercel environment. Destroy uses this state instead of the
/// current AppHost so projects can be cleaned up after resources are renamed or removed.
/// </summary>
internal sealed record VercelDeploymentState(
    int SchemaVersion,
    string Environment,
    string? Scope,
    string? Target,
    bool Production,
    VercelDeploymentStateEntry[] Deployments);

/// <summary>
/// Persisted ownership details for one Vercel project/deployment created or updated by Aspire.
/// </summary>
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

/// <summary>
/// Immutable VCR image reference paired with the digest Vercel Build Output API should deploy.
/// </summary>
internal sealed record VercelImageReference(string Reference, string Digest);

/// <summary>
/// Resource annotation produced during Vercel prerequisite work and consumed by Aspire's image
/// push decorator and deploy step to keep project, token, and image-tag context together.
/// </summary>
internal sealed record VercelPreparedDeploymentAnnotation(
    VercelDeploymentEntry Entry,
    VercelProjectLink ProjectLink,
    VercelPulledProjectContext ProjectContext,
    bool ManagedByAspire,
    string RemoteImageName,
    string RemoteImageTag,
    string TaggedImageReference) : IResourceAnnotation;

/// <summary>
/// Marker annotation used to attach Vercel push options only after project preparation has
/// produced provider-specific VCR image metadata.
/// </summary>
internal sealed class VercelImagePushOptionsCallbackAnnotation : IResourceAnnotation
{
}

/// <summary>
/// Resolved Vercel project identity, either from a checked-in link file or an Aspire-managed name.
/// </summary>
internal sealed record VercelProjectLink(string ProjectName, string? ProjectId);

/// <summary>
/// Project metadata and OIDC token materialized by <c>vercel pull</c> for one deployment.
/// </summary>
internal sealed record VercelPulledProject(
    string ProjectName,
    string? ProjectId,
    string? OrgId,
    string ProjectJsonContent,
    string OidcToken);

/// <summary>
/// Complete provider context needed after project preparation: environment variables, pulled
/// project settings, and decoded claims for VCR operations.
/// </summary>
internal sealed record VercelPulledProjectContext(
    VercelEnvironmentConfiguration EnvironmentConfiguration,
    VercelPulledProject PulledProject,
    VercelOidcClaims OidcClaims);

/// <summary>
/// Safe subset of <c>.vercel/project.json</c> identity fields that can be stored in state.
/// </summary>
internal sealed record VercelPulledProjectSettings(string ProjectName, string? ProjectId, string? OrgId);

/// <summary>
/// Vercel OIDC claims used for VCR routing and repository creation, not for local auth decisions.
/// </summary>
internal sealed record VercelOidcClaims(string? OwnerId, string? Owner, string? Project, string? ProjectId);
