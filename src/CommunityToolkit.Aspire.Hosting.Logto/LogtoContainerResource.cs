using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Logto;

/// <summary>
/// Represents a containerized resource specific to Logto that extends the base functionality
/// of container resources by providing additional endpoint and connection string management.
/// </summary>
/// <remarks>
/// This class is designed for use in an application hosting environment and incorporates
/// a primary HTTP endpoint with predefined default port configurations.
/// </remarks>
public sealed class LogtoContainerResource(string name)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string AdminEndpointName = "admin";
    internal const int DefaultHttpPort = 3001;
    internal const int DefaultHttpAdminPort = 3002;


    /// Gets the primary endpoint associated with the container resource.
    /// This property provides a reference to the primary HTTP endpoint for the resource,
    /// facilitating network communication and identifying the primary access point.
    /// The endpoint is tied to the default configuration for HTTP-based interactions
    /// and is predefined with a specific protocol and port settings.
    public EndpointReference PrimaryEndpoint => new(this, PrimaryEndpointName);

    /// Gets the host associated with the primary endpoint of the container resource.
    /// This property allows access to the host definition of the primary HTTP endpoint,
    /// which is referenced by using the `EndpointProperty.Host`.
    /// The Host provides necessary information for identifying the network address
    /// or location of the primary endpoint associated with this container resource.
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// Gets the port number associated with the primary HTTP endpoint of this resource.
    /// This property represents the port component of the endpoint where the resource
    /// is accessible. It is derived from the `PrimaryEndpoint` and corresponds to the
    /// value of the `Port` property in the endpoint configuration.
    /// The port is typically used to distinguish network services on the same host
    /// and is crucial in forming a valid connection string or URL for resource access.
    /// For example, this value may represent a default port for the application or
    /// a specific port explicitly configured for the resource's endpoint.
    /// This property is especially relevant when constructing connection strings or
    /// when validation of the endpoint's configuration is required.
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);


    private ReferenceExpression GetConnectionString()
    {
        var builder = new ReferenceExpressionBuilder();

        builder.Append(
            $"Endpoint={PrimaryEndpointName}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

        return builder.Build();
    }

    /// Gets the connection string expression for the Logto container resource.
    /// The connection string is dynamically constructed based on the resource's
    /// endpoint configuration and includes details such as the protocol, host,
    /// and port. This property provides a reference to the connection string,
    /// allowing integration with external resources or clients requiring
    /// connection details formatted as a string expression.
    public ReferenceExpression ConnectionStringExpression => GetConnectionString();
}