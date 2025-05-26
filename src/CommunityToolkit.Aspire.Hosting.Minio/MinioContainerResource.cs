namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a MiniO storage
/// </summary>
public sealed class MinioContainerResource : ContainerResource, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string ConsoleEndpointName = "console";
    internal const string DefaultUserName = "minioadmin";

    /// <summary>
    /// Initializes a new instance of the <see cref="MinioContainerResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="user">A parameter that contains the Minio server root user name.</param>
    /// <param name="password">A parameter that contains the Minio server root password.</param>
    public MinioContainerResource(string name, ParameterResource user, ParameterResource password) : base(name)
    {
        RootUser = user;
        PasswordParameter = password;
    }

    /// <summary>
    /// The MiniO root user.
    /// </summary>
    public ParameterResource RootUser { get; set; }

    /// <summary>
    /// The MiniO root password.
    /// </summary>
    public ParameterResource PasswordParameter { get; set; } 

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Minio. This endpoint is used for all API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
    
    /// <summary>
    /// Gets the connection string expression for the Minio
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => GetConnectionString();
    
    /// <summary>
    /// Gets the connection string for the MiniO server.
    /// </summary>
    /// <param name="cancellationToken"> A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the PostgreSQL server in the form "Host=host;Port=port;Username=postgres;Password=password".</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }
    
    /// <summary>
    /// Gets the connection string for the MiniO server.
    /// </summary>
    private ReferenceExpression GetConnectionString()
    {
        var builder = new ReferenceExpressionBuilder();

        builder.Append(
            $"Endpoint=http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

        builder.Append($";AccessKey={RootUser.Value}");
        builder.Append($";SecretKey={PasswordParameter.Value}");

        return builder.Build();
    }

    internal void SetPassword(ParameterResource password)
    {
        PasswordParameter = password;
    }
}