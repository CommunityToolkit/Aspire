namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents n8n
/// </summary>
public class N8nResource : ContainerResource, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    /// <summary>
    /// Initializes a new <see cref="N8nResource"/>.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="encryptionKeyParameter">A parameter that contains the n8n encryption key.</param>
    public N8nResource(string name, ParameterResource encryptionKeyParameter) : base(name)
    {
        EncryptionKeyParameter = encryptionKeyParameter;
    }

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the N8n. This endpoint is used for all API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the connection string expression for the N8n
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Scheme}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    /// <summary>
    /// Gets the connection URI expression for the N8n server.
    /// </summary>
    /// <remarks>
    /// Format: <c>{scheme}://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{PrimaryEndpoint.Scheme}://{Host}:{Port}");

    /// <summary>
    /// Gets the ParameterResource that identifies the encryption key used to protect sensitive data.
    /// </summary>
    public ParameterResource EncryptionKeyParameter { get; }

    /// <summary>
    /// Gets the ParameterResource that holds the plaintext instance owner password, or <see langword="null"/> if
    /// <see cref="N8nBuilderExtensions.WithInstanceOwner"/> has not been called.
    /// </summary>
    public ParameterResource? InstanceOwnerPassword { get; internal set; }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

