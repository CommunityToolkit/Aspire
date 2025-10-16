namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an InfluxDB container.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="userName">A parameter that contains the InfluxDB username.</param>
/// <param name="password">A parameter that contains the InfluxDB password.</param>
/// <param name="token">A parameter that contains the InfluxDB admin token.</param>
public class InfluxDBResource(
    string name,
    ParameterResource? userName,
    ParameterResource password,
    ParameterResource token) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    private const string DefaultUserName = "admin";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the InfluxDB. This endpoint is used for all API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the parameter that contains the InfluxDB username.
    /// </summary>
    public ParameterResource? UserNameParameter { get; } = userName;

    /// <summary>
    /// Gets the parameter that contains the InfluxDB password.
    /// </summary>
    public ParameterResource PasswordParameter { get; } = password;

    /// <summary>
    /// Gets the parameter that contains the InfluxDB admin token.
    /// </summary>
    public ParameterResource TokenParameter { get; } = token;

    internal ReferenceExpression UserNameReference =>
        UserNameParameter is not null ?
            ReferenceExpression.Create($"{UserNameParameter}") :
            ReferenceExpression.Create($"{DefaultUserName}");

    /// <summary>
    /// Gets the connection string expression for the InfluxDB.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Url=http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)};Token={TokenParameter}");
}
