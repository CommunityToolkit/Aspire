namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a container resource for LavinMQ with configurable authentication parameters
/// and connection endpoint details, implementing the IResourceWithConnectionString interface.
/// </summary>
/// <param name="name">The name of the LavinMQ resource instance.</param>
public class LavinMQContainerResource(string name)
    : ContainerResource(name), IResourceWithConnectionString
{
    private const string DefaultUserName = "guest";
    private const string DefaultPassword = "guest";
    internal const string PrimaryEndpointName = "amqp";
    internal const string ManagementEndpointName = "management";
    internal const string PrimaryEndpointSchema = "amqp";
    internal const string ManagementEndpointSchema = "http";
    internal const int DefaultAmqpPort = 5672;
    internal const int DefaultManagementPort = 15672;

    private EndpointReference? _primaryEndpoint;
    private EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    private static ReferenceExpression UserNameReference => ReferenceExpression.Create($"{DefaultUserName}");

    private static ReferenceExpression PasswordReference => ReferenceExpression.Create($"{DefaultPassword}");

    /// <summary>
    /// ConnectionString for the LavinMQ server in the form of amqp://guest:guest@host:port/.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"amqp://{UserNameReference}:{PasswordReference}@{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}/");

    /// <summary>
    /// Gets the connection URI expression for the LavinMQ server.
    /// </summary>
    /// <remarks>
    /// Format: <c>amqp://{user}:{password}@{host}:{port}/</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ConnectionStringExpression;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Username", UserNameReference);
        yield return new("Password", PasswordReference);
        yield return new("Uri", UriExpression);
    }
}