using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.RavenDB;

/// <summary>
/// A resource that references a RavenDB server or one of its databases.
/// </summary>
/// <param name="Resource">The application.</param>
/// <param name="ConnectionNames">Names of the referenced resources (the server, or its databases), which the client integration reads its settings under.</param>
/// <param name="Databases">The databases it references; a reference to the server reaches every database declared on it.</param>
/// <param name="ServersReferenced">How many RavenDB servers the application references in total.</param>
internal sealed record RavenDBConsumer(
    IResource Resource,
    IReadOnlyList<string> ConnectionNames,
    IReadOnlyList<string> Databases,
    int ServersReferenced);

/// <summary>Finds the applications that use a RavenDB server, for a deployment package.</summary>
internal static class RavenDBConsumers
{
    // What WithReference(...) records on the referencing resource; Aspire has no public constant for it.
    private const string ReferenceRelationship = "Reference";

    /// <summary>The resources that reference <paramref name="server"/> or one of its databases, except those left out of publish.</summary>
    public static IReadOnlyList<RavenDBConsumer> Find(DistributedApplicationModel model, RavenDBServerResource server)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(server);

        var consumers = new List<RavenDBConsumer>();

        foreach (var resource in model.Resources)
        {
            // A resource left out of publish is not deployed, so it needs no URL or certificate.
            if (resource is RavenDBServerResource or RavenDBDatabaseResource || resource.IsExcludedFromPublish())
            {
                continue;
            }

            var connectionNames = new SortedSet<string>(StringComparer.Ordinal);
            var databases = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var servers = new HashSet<RavenDBServerResource>();

            foreach (var referenced in References(resource))
            {
                var referencedServer = referenced switch
                {
                    RavenDBServerResource s => s,
                    RavenDBDatabaseResource d => d.Parent,
                    _ => null,
                };

                if (referencedServer is null)
                {
                    continue;
                }

                servers.Add(referencedServer);

                if (!ReferenceEquals(referencedServer, server))
                {
                    continue;
                }

                connectionNames.Add(referenced.Name);

                if (referenced is RavenDBDatabaseResource database)
                {
                    databases.Add(database.DatabaseName);
                }
                else
                {
                    databases.UnionWith(server.Databases.Values);
                }
            }

            if (connectionNames.Count > 0)
            {
                consumers.Add(new RavenDBConsumer(resource, [.. connectionNames], [.. databases], servers.Count));
            }
        }

        return consumers;
    }

    private static IEnumerable<IResource> References(IResource resource) =>
        resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(r => string.Equals(r.Type, ReferenceRelationship, StringComparison.Ordinal))
            .Select(r => r.Resource);
}
