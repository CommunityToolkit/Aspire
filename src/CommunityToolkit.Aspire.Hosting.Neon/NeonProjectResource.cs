namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Neon project.
/// </summary>
public class NeonProjectResource : ContainerResource, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "tcp";

    /// <param name="name">The name of the resource.</param>
    /// <param name="userName">A parameter that contains the Neon user name.</param>
    /// <param name="password">A parameter that contains the Neon password.</param>
    public NeonProjectResource(string name, ParameterResource userName, ParameterResource password) : base(name)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        
        UserNameParameter = userName;
        PasswordParameter = password;
    }

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Neon project.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the parameter that contains the Neon user name.
    /// </summary>
    public ParameterResource UserNameParameter { get; }

    /// <summary>
    /// Gets the parameter that contains the Neon password.
    /// </summary>
    public ParameterResource PasswordParameter { get; }

    /// <summary>
    /// Gets the connection string expression for the Neon project.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Host={PrimaryEndpoint.Property(EndpointProperty.Host)};Port={PrimaryEndpoint.Property(EndpointProperty.Port)};Username={UserNameParameter};Password={PasswordParameter}");
}
