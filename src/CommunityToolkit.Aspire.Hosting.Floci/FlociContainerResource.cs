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
    /// Gets the scheme endpoint reference for the primary endpoint. <c>http</c> unless a TLS
    /// certificate has been configured, in which case the endpoint is switched to <c>https</c>
    /// before the application starts. Floci serves both on the same port, so the port never changes.
    /// </summary>
    public EndpointReferenceExpression Scheme => PrimaryEndpoint.Property(EndpointProperty.Scheme);

    /// <summary>
    /// Gets the emulator endpoint URL, following the primary endpoint's <see cref="Scheme"/>.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Scheme}://{Host}:{Port}");

    /// <summary>
    /// Gets the emulator endpoint URL pinned to <c>http</c>, for the Floci UI sidecar.
    /// </summary>
    /// <remarks>
    /// Every Floci image serves HTTP and HTTPS simultaneously on the same port, so this stays valid
    /// with TLS enabled. The UI must not follow <see cref="Scheme"/>: it reaches the emulator by its
    /// container-network name, and neither the ASP.NET Core development certificate (SAN
    /// <c>localhost</c> only) nor a certificate issued for the host covers that name — so HTTPS would
    /// fail hostname validation even after the trust bundle is installed. The hop is container-to-
    /// container on Aspire's internal network, where TLS buys nothing.
    /// </remarks>
    internal ReferenceExpression UIEndpointExpression =>
        ReferenceExpression.Create($"http://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", ConnectionStringExpression);
    }

    /// <summary>
    /// Sets the Floci UI environment variables needed for this cloud's adapter to connect.
    /// Implemented by each concrete cloud resource so <c>WithFlociUI</c> and the UI's <c>WithReference</c> overloads
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

    /// <summary>
    /// Gets the names of the env vars this cloud's image reads to configure its TLS listener, or
    /// <see langword="null"/> when the image has no TLS support. Backs <c>ConfigureTlsCore</c>, which
    /// is only wired up for the resource types that return a value here.
    /// </summary>
    internal virtual CommunityToolkit.Aspire.Hosting.Floci.FlociTlsEnvVars? TlsEnvVars => null;
}

