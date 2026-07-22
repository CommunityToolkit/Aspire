#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Common base for Floci cloud-emulator container resources (AWS, Azure, GCP).
/// Holds the shared endpoint/connection-string plumbing so each cloud only needs to
/// implement its own image, container env vars, and Floci UI wiring.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="endpointName">The name of the primary HTTP endpoint for this cloud's emulator.</param>
public abstract class FlociContainerResource(string name, string endpointName) : ContainerResource(name), IResourceWithConnectionString
{
    private EndpointReference? _primaryEndpoint;

    internal string EndpointName { get; } = endpointName;

    /// <summary>
    /// Gets the primary endpoint reference for the Floci container.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, EndpointName);

    /// <summary>
    /// Gets the host endpoint reference for the primary endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for the primary endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the emulator endpoint URL.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"http://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", ConnectionStringExpression);
    }

    /// <summary>
    /// Sets the Floci UI environment variables needed for this cloud's adapter to connect.
    /// Implemented by each concrete cloud resource so <c>WithFlociUI</c> and <c>WithPluggedCloud</c>
    /// can attach any combination of clouds to a single shared UI container.
    /// </summary>
    internal abstract void ApplyUIEnvironment(EnvironmentCallbackContext context);

    /// <summary>
    /// Gets the name of the env var this cloud's image reads to locate the Docker socket.
    /// Backs the shared <c>WithDockerSocket</c> implementation used by all three providers.
    /// </summary>
    internal abstract string DockerHostEnvVar { get; }

    /// <summary>
    /// Gets the name of the env var this cloud's image reads to select its storage mode
    /// (memory vs. persistent). Backs the shared <c>WithDataVolume</c>/<c>WithDataBindMount</c>
    /// implementation used by all three providers.
    /// </summary>
    internal abstract string StorageModeEnvVar { get; }
}

#pragma warning restore ASPIREATS001
