namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a MiniO storage
/// </summary>
/// <param name="name">The name of the resource</param>
/// <param name="rootUser"> A parameter that contains the MiniO server admin user name, or null to</param>
/// <param name="rootPassword"> A parameter that contains the Minio server admin password</param>
public sealed class MinioContainerResource(
    string name,
    ParameterResource? rootUser,
    ParameterResource rootPassword) : ContainerResource(name),
    IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string DefaultUserName = "admin";

    /// <summary>
    /// The MiniO root user.
    /// </summary>
    public ParameterResource? RootUser { get; set; } = rootUser;

    /// <summary>
    /// The MiniO root password.
    /// </summary>
    public ParameterResource RootPassword { get; } = rootPassword;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Minio. This endpoint is used for all API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    internal ReferenceExpression RootUserNameReference =>
        RootUser is not null ?
            ReferenceExpression.Create($"{RootUser}") :
            ReferenceExpression.Create($"{DefaultUserName}");
    
    /// <summary>
    /// Gets the connection string expression for the Minio
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Endpoint=http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");
}