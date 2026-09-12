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
    IReadOnlyList<KeyValuePair<string, string>> ProjectEnvironmentVariables,
    IReadOnlyList<VercelServiceBinding> ServiceBindings)
{
    public static VercelEnvironmentConfiguration Empty { get; } = new([], [], []);

    public IEnumerable<string> AllEnvironmentVariableNames =>
        DeploymentEnvironmentVariables.Select(static variable => variable.Key)
            .Concat(ProjectEnvironmentVariables.Select(static variable => variable.Key))
            .Concat(ServiceBindings.Select(static binding => binding.EnvironmentVariableName));
}

/// <summary>
/// Describes one Vercel service binding that injects a private target service URL into the caller.
/// </summary>
internal sealed record VercelServiceBinding(string EnvironmentVariableName, string ServiceName);

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

    public VercelServiceDeploymentStateEntry[] Services { get; init; } = [];

    public int? BuildOutputApiVersion { get; init; }

    public string[] ProjectEnvironmentVariables { get; init; } = [];
}

/// <summary>
/// Immutable VCR image reference paired with the digest Vercel Build Output API should deploy.
/// </summary>
internal sealed record VercelImageReference(string Reference, string Digest);

/// <summary>
/// Captures one Aspire workload as a Vercel service within a project group.
/// </summary>
internal sealed record VercelDeploymentService(
    VercelDeploymentEntry Entry,
    string ServiceName,
    bool IsPublicRoot);

/// <summary>
/// A Vercel project deployment unit: one public/root service plus private services it owns.
/// </summary>
internal sealed record VercelDeploymentProjectGroup(
    VercelDeploymentService Root,
    VercelDeploymentService[] Services)
{
    public VercelDeploymentEntry RootEntry => Root.Entry;
}

/// <summary>
/// Lookup structure used while translating Aspire references into Vercel project/service concepts.
/// </summary>
internal sealed class VercelDeploymentProjectMap(IReadOnlyList<VercelDeploymentProjectGroup> groups)
{
    private readonly Dictionary<string, VercelDeploymentProjectGroup> _groupsByResourceName = groups
        .SelectMany(group => group.Services.Select(service => new { service.Entry.Resource.Name, Group = group }))
        .ToDictionary(static item => item.Name, static item => item.Group, StringComparer.Ordinal);

    private readonly Dictionary<string, VercelDeploymentService> _servicesByResourceName = groups
        .SelectMany(static group => group.Services)
        .ToDictionary(static service => service.Entry.Resource.Name, StringComparer.Ordinal);

    public IReadOnlyList<VercelDeploymentProjectGroup> Groups { get; } = groups;

    public bool TryGetService(string resourceName, out VercelDeploymentService service)
        => _servicesByResourceName.TryGetValue(resourceName, out service!);

    public bool TryGetGroup(string resourceName, out VercelDeploymentProjectGroup group)
        => _groupsByResourceName.TryGetValue(resourceName, out group!);

    public bool AreInSameProject(string sourceResourceName, string targetResourceName)
        => _groupsByResourceName.TryGetValue(sourceResourceName, out var sourceGroup)
            && _groupsByResourceName.TryGetValue(targetResourceName, out var targetGroup)
            && ReferenceEquals(sourceGroup, targetGroup);
}

/// <summary>
/// Pairs a prepared Vercel service with the immutable image digest resolved after Aspire pushes it.
/// </summary>
internal sealed record VercelResolvedDeployment(
    VercelPreparedDeploymentAnnotation PreparedDeployment,
    VercelImageReference Image);

/// <summary>
/// Persisted digest/repository details for one service inside a Vercel project deployment.
/// </summary>
internal sealed record VercelServiceDeploymentStateEntry(
    string ResourceName,
    string ServiceName,
    string? VcrImageDigest);

/// <summary>
/// Resource annotation produced during Vercel prerequisite work and consumed by Aspire's image
/// push decorator and deploy step to keep project, token, and image-tag context together.
/// </summary>
internal sealed record VercelPreparedDeploymentAnnotation(
    VercelDeploymentEntry Entry,
    string ServiceName,
    VercelProjectLink ProjectLink,
    VercelPulledProjectContext ProjectContext,
    VercelEnvironmentConfiguration EnvironmentConfiguration,
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
