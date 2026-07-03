#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPROBES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Projects Aspire compute resources into the subset of the app model that Vercel Dockerfile
/// hosting can represent, and rejects unsupported constructs before provider mutation starts.
/// </summary>
internal static class VercelDeploymentModel
{
    private static readonly string[] VercelJsonBuildOutputUnsupportedKeys =
    [
        "build",
        "builds",
        "buildCommand",
        "devCommand",
        "env",
        "experimentalServices",
        "experimentalServicesV2",
        "framework",
        "functions",
        "headers",
        "ignoreCommand",
        "installCommand",
        "outputDirectory",
        "redirects",
        "rewrites",
        "routes",
        "services"
    ];

    public static IReadOnlyDictionary<string, VercelDeploymentEntry> GetEntriesByResourceName(IReadOnlyList<VercelDeploymentEntry> entries)
        => entries.ToDictionary(static entry => entry.Resource.Name, StringComparer.Ordinal);

    public static IEnumerable<VercelDeploymentEntry> GetEntries(DistributedApplicationModel model, VercelEnvironmentResource environment)
    {
        var computeEnvironments = model.Resources.OfType<IComputeEnvironmentResource>().Take(2).ToArray();
        // Match Aspire's single-environment convention only when Vercel is the sole compute
        // environment. With mixed targets, implicit selection would accidentally deploy
        // untargeted resources to every environment.
        bool allowImplicitTargeting = computeEnvironments.Length == 1 && ReferenceEquals(computeEnvironments[0], environment);

        foreach (var resource in model.Resources.OfType<IComputeResource>())
        {
            if (!IsTargetedToEnvironment(resource, environment, allowImplicitTargeting))
            {
                continue;
            }

            if (resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile))
            {
                yield return new(resource, dockerfile.ContextPath, dockerfile.DockerfilePath, dockerfile);
                continue;
            }

            if (resource is ProjectResource project)
            {
                string projectPath = project.GetProjectMetadata().ProjectPath;
                string sourceRoot = Path.GetDirectoryName(projectPath)
                    ?? throw new DistributedApplicationException($"Project resource '{resource.Name}' has project path '{projectPath}' without a containing directory.");
                yield return new(resource, sourceRoot);
                continue;
            }

            throw new DistributedApplicationException($"Resource '{resource.Name}' targets Vercel but is not an Aspire image build resource. Use a .NET project, a workload integration that publishes Dockerfile metadata, call PublishAsDockerFile, or configure the resource with WithDockerfile, WithDockerfileFactory, or WithDockerfileBuilder.");
        }
    }

    public static void ValidateEntries(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        // Keep unsupported Vercel-preview cases in validation so publish/prereq/deploy fail
        // before mutating provider projects. Each failure names the Aspire concept that
        // cannot be projected rather than letting a later Vercel CLI call fail opaquely.
        if (entries.Count == 0)
        {
            throw new DistributedApplicationException("No image-build compute resources target Vercel. Add a .NET project, a workload with Aspire Dockerfile publish metadata, or use WithComputeEnvironment to target Vercel when multiple compute environments are present.");
        }

        foreach (var entry in entries)
        {
            if (!Directory.Exists(entry.SourceRoot))
            {
                throw new DistributedApplicationException($"The Vercel source root '{entry.SourceRoot}' for resource '{entry.Resource.Name}' does not exist.");
            }

            if (entry.Dockerfile is { DockerfileFactory: null } && !File.Exists(entry.DockerfilePath!))
            {
                throw new DistributedApplicationException($"The Vercel Dockerfile '{entry.DockerfilePath}' for resource '{entry.Resource.Name}' does not exist. Configure the resource with an existing Dockerfile or Aspire-generated Dockerfile metadata.");
            }

            ValidateUnsupportedResourceModel(entry);
        }

        ValidateUniqueProjectNames(entries);
    }

    public static async Task<VercelDeploymentEntry> PrepareEntryAsync(PipelineStepContext context, VercelDeploymentEntry entry)
    {
        await ValidateVercelJsonAsync(entry.Resource, entry.SourceRoot, context.CancellationToken).ConfigureAwait(false);

        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string tempDirectory = outputService.GetTempDirectory(entry.Resource);
        string deployDirectory = Path.Combine(tempDirectory, "vercel-build-output");
        Directory.CreateDirectory(tempDirectory);
        if (Directory.Exists(deployDirectory))
        {
            Directory.Delete(deployDirectory, recursive: true);
        }

        // This is an output-only Build Output API root, not a staged copy of the source.
        // Aspire's built-in build/push steps read the real source/project directly; Vercel
        // deploy receives only generated metadata that points at the digest already pushed to VCR.
        Directory.CreateDirectory(deployDirectory);

        return entry with
        {
            TempDirectory = tempDirectory,
            DeployDirectory = deployDirectory
        };
    }

    public static async Task ValidateVercelJsonAsync(
        IResource resource,
        string sourceRoot,
        CancellationToken cancellationToken)
    {
        // User vercel.json can coexist only when it does not claim the same Build Output API
        // routes/functions/services surface this integration generates under .vercel/output.
        // See https://vercel.com/docs/project-configuration and https://vercel.com/docs/build-output-api.
        string vercelJsonPath = Path.Combine(sourceRoot, VercelConstants.JsonFileName);
        if (!File.Exists(vercelJsonPath))
        {
            return;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(vercelJsonPath, cancellationToken).ConfigureAwait(false)) as JsonObject
                ?? throw new DistributedApplicationException($"Resource '{resource.Name}' source root contains '{VercelConstants.JsonFileName}', but it is not a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException($"Resource '{resource.Name}' source root contains invalid '{VercelConstants.JsonFileName}'.", ex);
        }

        var unsupportedKey = VercelJsonBuildOutputUnsupportedKeys.FirstOrDefault(root.ContainsKey);
        if (unsupportedKey is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' source root contains '{VercelConstants.JsonFileName}' with top-level '{unsupportedKey}', but the Aspire Vercel integration owns the generated Build Output API container function and catch-all routing configuration. Move that setting into the Dockerfile, AppHost environment variables, or Vercel project settings before deploying with the Aspire Vercel integration.");
        }
    }

    public static string GetDisplayDockerfilePath(VercelDeploymentEntry entry)
        => entry.Dockerfile is null
            ? "<project container>"
            : entry.Dockerfile.DockerfileFactory is null
                ? Path.GetRelativePath(entry.SourceRoot, entry.DockerfilePath!)
                : "<generated>";

    public static bool IsHttpEndpoint(EndpointAnnotation endpoint)
        // URI scheme alone is not enough: Aspire can model a TCP transport with an HTTP
        // URI scheme. Vercel's container ingress here is HTTP-family traffic over TCP.
        => endpoint.Protocol == ProtocolType.Tcp
            && (string.Equals(endpoint.UriScheme, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.UriScheme, "https", StringComparison.OrdinalIgnoreCase))
            && (string.Equals(endpoint.Transport, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.Transport, "http2", StringComparison.OrdinalIgnoreCase));

    private static bool IsTargetedToEnvironment(IResource resource, VercelEnvironmentResource environment, bool allowImplicitTargeting)
    {
        var computeEnvironment = resource.GetComputeEnvironment();
        // Match Aspire's single-environment convention: when Vercel is the only compute
        // environment, image-build workloads implicitly target it.
        return ReferenceEquals(computeEnvironment, environment)
            || (computeEnvironment is null && allowImplicitTargeting);
    }

    private static void ValidateUniqueProjectNames(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        // Production endpoint references use https://{projectName}.vercel.app. If two
        // resources resolve to the same Vercel project, endpoint references and destroy
        // ownership would both become ambiguous.
        var projectNames = entries
            .Select(entry => new
            {
                Entry = entry,
                ProjectLink = VercelProjectNameResolver.GetProjectLink(entry)
            })
            .GroupBy(item => item.ProjectLink.ProjectName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();

        if (projectNames.Length == 0)
        {
            return;
        }

        var collision = projectNames[0];
        string resources = string.Join(", ", collision.Select(static item => $"'{item.Entry.Resource.Name}'").Order(StringComparer.Ordinal));
        throw new DistributedApplicationException(
            $"Multiple Vercel resources resolve to project name '{collision.Key}' ({resources}). Vercel project names must be unique per environment because each resource deploys to and references one project production URL. Use WithVercelProjectName, distinct source directory names, or link each resource to a distinct Vercel project with .vercel/project.json.");
    }

    private static void ValidateUnsupportedResourceModel(VercelDeploymentEntry entry)
    {
        IResource resource = entry.Resource;

        // This method is intentionally conservative. Each rejected annotation has run-mode
        // or another deployment-target semantics that Vercel's Dockerfile deploy cannot
        // project without changing what the user modeled in the AppHost. If Vercel gains a
        // native equivalent later, add the mapping and tests here instead of silently ignoring it.
        if (resource.Annotations.OfType<ContainerRegistryReferenceAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire generic container registry/image push metadata, but Vercel deployments use Vercel Container Registry through a provider-owned prebuilt artifact. Remove WithContainerRegistry before deploying with the Aspire Vercel integration.");
        }

        if (resource.Annotations.OfType<ContainerMountAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire container volumes or bind mounts, but Vercel Dockerfile deployments do not support Aspire-managed container mounts. Move persistent state to a Vercel-supported external service or remove the mount.");
        }

        if (resource.Annotations.OfType<ContainerFilesSourceAnnotation>().Any()
            || resource.Annotations.OfType<ContainerFilesDestinationAnnotation>().Any()
            || resource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire container file mounts, but Vercel Dockerfile deployments build the checked-in source tree directly. Include required files in the source tree or deploy this resource outside the Aspire Vercel integration.");
        }

        if (resource.Annotations.OfType<ProbeAnnotation>().Any()
            || resource.Annotations.OfType<EndpointProbeAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire container probes, but the Vercel preview integration does not map them to Vercel-native checks. Remove the Aspire probes or configure health behavior in Vercel.");
        }

        ValidateEndpointModel(entry);
        ValidateProjectName(entry);
    }

    private static void ValidateEndpointModel(VercelDeploymentEntry entry)
    {
        var endpoints = entry.Resource.Annotations.OfType<EndpointAnnotation>().ToArray();

        if (endpoints.Length == 0)
        {
            return;
        }

        // Reject the tempting Compose/ACA shapes up front: private listeners, multiple
        // target ports, and non-HTTP protocols do not have an equivalent in this preview's
        // single public Vercel container ingress.
        // Vercel's Dockerfile preview exposes one public platform ingress; it has no
        // Aspire-modeled private service network for internal endpoints.
        // See https://vercel.com/docs/functions/container-images.
        var internalEndpoint = endpoints.FirstOrDefault(static endpoint => !endpoint.IsExternal);
        if (internalEndpoint is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures endpoint '{internalEndpoint.Name}' as internal, but Vercel Dockerfile deployments expose public platform HTTPS ingress only. Mark the endpoint external or remove it before deploying to Vercel.");
        }

        var unsupportedEndpoint = endpoints.FirstOrDefault(static endpoint => !IsHttpEndpoint(endpoint));
        if (unsupportedEndpoint is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures endpoint '{unsupportedEndpoint.Name}' with scheme '{unsupportedEndpoint.UriScheme}' and transport '{unsupportedEndpoint.Transport}', but Vercel Dockerfile deployments support only HTTP or HTTPS endpoints with HTTP transports.");
        }

        var targetPorts = endpoints
            .Select(static endpoint => endpoint.TargetPort)
            .Where(static targetPort => targetPort.HasValue)
            .Select(static targetPort => targetPort!.Value)
            .Distinct()
            .ToArray();

        // Vercel provides one runtime listener through $PORT. Additional target ports would
        // look like ACA extra ports, but Vercel has no equivalent modeled here.
        if (targetPorts.Length > 1)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures multiple Aspire endpoint target ports, but Vercel Dockerfile deployments support only one HTTP listening port exposed through the $PORT environment variable.");
        }
    }

    private static void ValidateProjectName(VercelDeploymentEntry entry)
    {
        if (VercelProjectNameResolver.HasProjectLinkFile(entry.SourceRoot))
        {
            return;
        }

        _ = VercelProjectNameResolver.GetProjectName(entry);
    }
}
