using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.HashiCorp.Vault;

/// <summary>
/// Represents a sealed container resource for HashiCorp Vault integration within a distributed application.
/// It provides properties and methods to define endpoints, configure environment variables,
/// and handle connection strings associated with the Vault service.
/// </summary>
public sealed class VaultResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    /// <summary>
    /// Represents the name of the HTTP endpoint for a Vault resource.
    /// </summary>
    /// <remarks>
    /// This constant is used to define and reference the primary HTTP endpoint
    /// associated with the Vault container during its configuration and runtime setup.
    /// It is utilized in conjunction with network-related operations, such as endpoint
    /// and scheme configuration for the system resources.
    /// </remarks>
    internal const string HttpEndpointName = "http";

    /// <summary>
    /// The default port used for the Vault service communication.
    /// </summary>
    /// <remarks>
    /// The <c>DefaultPort</c> is set to <c>8200</c>, which is the standard port number
    /// for HashiCorp Vault services. This value is used as the fallback or standard
    /// port when no custom port is explicitly specified.
    /// </remarks>
    internal const int DefaultPort = 8200;

    /// <summary>
    /// Stores a reference to the primary endpoint for the Vault resource. This property is used to manage and retrieve
    /// information about the primary endpoint of the resource, such as its scheme, host, and port.
    /// </summary>
    private EndpointReference? _primaryEndpointReference;

    /// <summary>
    /// Gets the primary endpoint of the Vault resource.
    /// </summary>
    /// <remarks>
    /// The primary endpoint is used to define the main HTTP connection details for the Vault resource.
    /// This includes essential network attributes such as scheme, host, and port, which facilitate
    /// the generation of a connection string for interacting with the Vault service.
    /// </remarks>
    public EndpointReference PrimaryEndpoint =>
        _primaryEndpointReference ??= new EndpointReference(this, HttpEndpointName);

    /// <summary>
    /// Represents the parameter resource holding the root token for the Vault connection.
    /// This parameter is optional and can be used to specify the authentication token required to access the Vault.
    /// If provided, it will be included as part of the environment variables under the "VAULT_TOKEN" key.
    /// </summary>
    public ParameterResource? RootTokenParameter { get; set; }

    /// <summary>
    /// Gets the expression representing the connection string for the Vault resource.
    /// </summary>
    /// <remarks>
    /// The connection string is dynamically constructed based on the primary endpoint's scheme,
    /// host, and port properties. It is used to configure the Vault resource connection
    /// and is also exposed as part of the environment variables for the resource.
    /// </remarks>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
        );

    /// <summary>
    /// Gets a collection of environment variables required to connect to the Vault resource.
    /// These environment variables typically include:
    /// - "VAULT_ADDR": The URL of the Vault server derived from the connection string.
    /// - "VAULT_TOKEN": The root token used for authentication, if a RootTokenParameter is specified.
    /// </summary>
    public IEnumerable<KeyValuePair<string, ReferenceExpression>> EnvironmentVariables
    {
        get
        {
            yield return new KeyValuePair<string, ReferenceExpression>("VAULT_ADDR", ConnectionStringExpression);
            if (RootTokenParameter is not null)
                yield return new KeyValuePair<string, ReferenceExpression>("VAULT_TOKEN",
                    ReferenceExpression.Create($"{RootTokenParameter}"));
        }
    }
}