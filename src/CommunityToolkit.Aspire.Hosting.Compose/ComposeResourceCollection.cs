using System.Collections;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Compose;

/// <summary>
/// Represents a collection of Aspire resources created from a Docker Compose file.
/// Provides indexed access to resources by their compose service name.
/// </summary>
public sealed class ComposeResourceCollection : IEnumerable<KeyValuePair<string, IResourceBuilder<ContainerResource>>>
{
    private readonly Dictionary<string, IResourceBuilder<ContainerResource>> _resources;

    /// <summary>
    /// Gets the path to the source compose file.
    /// </summary>
    public string ComposePath { get; }

    /// <summary>
    /// Gets the names of all services in this compose file.
    /// </summary>
    public IReadOnlyCollection<string> ServiceNames => _resources.Keys;

    /// <summary>
    /// Gets the number of services in this compose file.
    /// </summary>
    public int Count => _resources.Count;

    internal ComposeResourceCollection(string composePath, Dictionary<string, IResourceBuilder<ContainerResource>> resources)
    {
        ComposePath = composePath;
        _resources = resources;
    }

    /// <summary>
    /// Gets the Aspire resource builder for the specified compose service name.
    /// </summary>
    /// <param name="serviceName">The name of the service as defined in the compose file.</param>
    /// <returns>The <see cref="IResourceBuilder{ContainerResource}"/> for the service.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the service name is not found.</exception>
    public IResourceBuilder<ContainerResource> this[string serviceName] =>
        _resources.TryGetValue(serviceName, out IResourceBuilder<ContainerResource>? resource)
            ? resource
            : throw new KeyNotFoundException($"Service '{serviceName}' not found in compose file '{ComposePath}'. " + $"Available services: {string.Join(", ", _resources.Keys)}");

    /// <summary>
    /// Tries to get the resource builder for the specified service name.
    /// </summary>
    public bool TryGetResource(string serviceName, out IResourceBuilder<ContainerResource>? resource) => _resources.TryGetValue(serviceName, out resource);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, IResourceBuilder<ContainerResource>>> GetEnumerator() => _resources.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
