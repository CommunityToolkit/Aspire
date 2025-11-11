namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a MinIO storage
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="rootUser">A parameter that contains the MinIO server root username.</param>
/// <param name="passwordParameter">A parameter that contains the MinIO server root password.</param>
public sealed class MinioContainerResource(string name, ParameterResource rootUser, ParameterResource passwordParameter) : ContainerResource(name),
    IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string ConsoleEndpointName = "console";
    internal const string DefaultUserName = "minioadmin";

    /// <summary>
    /// The MinIO root user.
    /// </summary>
    public ParameterResource RootUser { get; set; } = rootUser;

    /// <summary>
    /// The MinIO root password.
    /// </summary>
    public ParameterResource PasswordParameter { get; private set; } = passwordParameter;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the MinIO. This endpoint is used for all API calls over HTTP.
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
    /// Gets the connection string expression for the Minio
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => GetConnectionString();

    /// <summary>
    /// Gets the connection URI expression for the MinIO server.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"http://{Host}:{Port}");

    /// <summary>
    /// Gets the connection string for the MinIO server.
    /// </summary>
    /// <param name="cancellationToken"> A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the MinIO server in the form "Host=host;Port=port;Username=postgres;Password=password".</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the connection string for the MinIO server.
    /// </summary>
    private ReferenceExpression GetConnectionString()
    {
        var builder = new ReferenceExpressionBuilder();

        builder.Append(
            $"Endpoint=http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

        builder.Append($";AccessKey={RootUser}");
        builder.Append($";SecretKey={PasswordParameter}");

        return builder.Build();
    }

    internal void SetPassword(ParameterResource password)
    {
        PasswordParameter = password;
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("AccessKey", ReferenceExpression.Create($"{RootUser}"));
        yield return new("SecretKey", ReferenceExpression.Create($"{PasswordParameter}"));
        yield return new("Uri", UriExpression);
    }
}