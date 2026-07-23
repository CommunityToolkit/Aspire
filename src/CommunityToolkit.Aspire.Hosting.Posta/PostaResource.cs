namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Posta email delivery platform.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="jwtSecret">A parameter that contains the JWT signing secret.</param>
/// <param name="adminPassword">A parameter that contains the initial admin password.</param>
[AspireExport(ExposeProperties = true)]
public sealed class PostaResource(string name, ParameterResource jwtSecret, ParameterResource adminPassword)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const int HttpEndpointPort = 9000;
    internal const string HttpEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary HTTP endpoint for the Posta server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for the HTTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for the HTTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the parameter that contains the JWT signing secret.
    /// </summary>
    public ParameterResource JwtSecretParameter { get; } = jwtSecret ?? throw new ArgumentNullException(nameof(jwtSecret));

    /// <summary>
    /// Gets the parameter that contains the initial admin password.
    /// </summary>
    public ParameterResource AdminPasswordParameter { get; } = adminPassword ?? throw new ArgumentNullException(nameof(adminPassword));

    /// <summary>
    /// Connection string for the Posta HTTP API in the form of Endpoint=http://host:port.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"Endpoint={PrimaryEndpoint.Property(EndpointProperty.Url)}");

    /// <summary>
    /// Gets the connection URI expression for the Posta HTTP API.
    /// </summary>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Url)}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

