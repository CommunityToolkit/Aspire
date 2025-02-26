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

    private static ReferenceExpression UserNameReference => ReferenceExpression.Create($"{DefaultUserName}");

    private static ReferenceExpression PasswordReference => ReferenceExpression.Create($"{DefaultPassword}");

    /// <summary>
    /// ConnectionString for the LavinMQ server in the form of amqp://guest:guest@host:port/.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"amqp://{UserNameReference}:{PasswordReference}@{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}/");
}