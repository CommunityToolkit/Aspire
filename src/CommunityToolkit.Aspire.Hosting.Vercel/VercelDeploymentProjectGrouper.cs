#pragma warning disable ASPIREPIPELINES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Converts the flat set of Vercel-targeted Aspire resources into Vercel project groups.
/// </summary>
internal static class VercelDeploymentProjectGrouper
{
    public static async Task<VercelDeploymentProjectMap> CreateMapAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> referencesByResourceName = new(StringComparer.Ordinal);
        foreach (var entry in entries)
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

            referencesByResourceName[entry.Resource.Name] = [.. VercelEnvironmentMapper.GetReferencedResourceNames(entry.Resource, executionConfiguration)
                .Where(name => entries.Any(candidate => string.Equals(candidate.Resource.Name, name, StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal)];
        }

        return CreateMap(entries, referencesByResourceName);
    }

    public static VercelDeploymentProjectMap CreateMap(
        IReadOnlyList<VercelDeploymentEntry> entries,
        IReadOnlyDictionary<string, string[]> referencesByResourceName)
    {
        if (entries.Count == 1)
        {
            var single = CreateService(entries[0], isPublicRoot: true);
            return new([new(single, [single])]);
        }

        var entriesByName = entries.ToDictionary(static entry => entry.Resource.Name, StringComparer.Ordinal);
        var publicRoots = entries
            .Where(HasPublicHttpEndpoint)
            .Select(entry => CreateService(entry, isPublicRoot: true))
            .ToArray();

        if (publicRoots.Length == 0)
        {
            throw new DistributedApplicationException("Multiple resources target Vercel, but none exposes an external HTTP endpoint that can be used as a Vercel project root. Add an external HTTP endpoint to the public workload or deploy a single resource.");
        }

        ValidateUniqueProjectNames(publicRoots);
        ValidateUniqueServiceNames(entries);

        var publicRootNames = publicRoots
            .Select(static root => root.Entry.Resource.Name)
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, List<VercelDeploymentService>> servicesByRootName = publicRoots
            .ToDictionary(static root => root.Entry.Resource.Name, static root => new List<VercelDeploymentService> { root }, StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (publicRootNames.Contains(entry.Resource.Name))
            {
                continue;
            }

            if (entry.Resource.TryGetLastAnnotation<VercelProjectOptionsAnnotation>(out _))
            {
                throw new DistributedApplicationException($"Resource '{entry.Resource.Name}' is an internal Vercel service because it is owned by a public project root. WithVercelProjectName can only be applied to public Vercel project roots.");
            }

            var owners = publicRoots
                .Where(root => ReferencesResourceTransitively(root.Entry.Resource.Name, entry.Resource.Name, referencesByResourceName, publicRootNames))
                .ToArray();

            if (owners.Length == 0)
            {
                throw new DistributedApplicationException($"Resource '{entry.Resource.Name}' targets Vercel but is not public and is not referenced by any public Vercel project root. Add an external HTTP endpoint to make it a public project, reference it from one public workload, or deploy it separately.");
            }

            if (owners.Length > 1)
            {
                string ownerNames = string.Join(", ", owners.Select(static owner => $"'{owner.Entry.Resource.Name}'").Order(StringComparer.Ordinal));
                throw new DistributedApplicationException($"Resource '{entry.Resource.Name}' is referenced by multiple public Vercel project roots ({ownerNames}). Shared internal Vercel services are ambiguous; deploy the service separately or reference it from exactly one public workload.");
            }

            servicesByRootName[owners[0].Entry.Resource.Name].Add(CreateService(entry, isPublicRoot: false));
        }

        List<VercelDeploymentProjectGroup> groups = [];
        foreach (var root in publicRoots)
        {
            groups.Add(new(root, [.. servicesByRootName[root.Entry.Resource.Name].OrderBy(static service => service.IsPublicRoot ? 0 : 1).ThenBy(static service => service.ServiceName, StringComparer.Ordinal)]));
        }

        return new(groups);
    }

    private static VercelDeploymentService CreateService(VercelDeploymentEntry entry, bool isPublicRoot)
        => new(entry, VercelProjectNameResolver.GetServiceName(entry.Resource), isPublicRoot);

    private static bool HasPublicHttpEndpoint(VercelDeploymentEntry entry)
        => entry.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Any(static endpoint => endpoint.IsExternal && VercelDeploymentModel.IsHttpEndpoint(endpoint));

    private static bool ReferencesResourceTransitively(
        string sourceResourceName,
        string targetResourceName,
        IReadOnlyDictionary<string, string[]> referencesByResourceName,
        HashSet<string> publicRootNames)
    {
        Queue<string> queue = [];
        HashSet<string> visited = new(StringComparer.Ordinal);
        queue.Enqueue(sourceResourceName);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!visited.Add(current)
                || !referencesByResourceName.TryGetValue(current, out string[]? references))
            {
                continue;
            }

            foreach (string reference in references)
            {
                if (string.Equals(reference, targetResourceName, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!publicRootNames.Contains(reference))
                {
                    queue.Enqueue(reference);
                }
            }
        }

        return false;
    }

    private static void ValidateUniqueProjectNames(IReadOnlyList<VercelDeploymentService> publicRoots)
    {
        var projectNames = publicRoots
            .Select(root => new
            {
                Root = root,
                ProjectLink = VercelProjectNameResolver.GetProjectLink(root.Entry)
            })
            .GroupBy(item => item.ProjectLink.ProjectName, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .ToArray();

        if (projectNames.Length == 0)
        {
            return;
        }

        var collision = projectNames[0];
        string resources = string.Join(", ", collision.Select(static item => $"'{item.Root.Entry.Resource.Name}'").Order(StringComparer.Ordinal));
        throw new DistributedApplicationException($"Multiple public Vercel project roots resolve to project name '{collision.Key}' ({resources}). Use WithVercelProjectName, distinct source directory names, or link each root to a distinct Vercel project with .vercel/project.json.");
    }

    private static void ValidateUniqueServiceNames(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        var serviceNames = entries
            .Select(static entry => new
            {
                Entry = entry,
                ServiceName = VercelProjectNameResolver.GetServiceName(entry.Resource)
            })
            .GroupBy(static item => item.ServiceName, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .ToArray();

        if (serviceNames.Length == 0)
        {
            return;
        }

        var collision = serviceNames[0];
        string resources = string.Join(", ", collision.Select(static item => $"'{item.Entry.Resource.Name}'").Order(StringComparer.Ordinal));
        throw new DistributedApplicationException($"Multiple Vercel services resolve to service name '{collision.Key}' ({resources}). Rename one of the Aspire resources so each Vercel service has a unique name.");
    }
}
