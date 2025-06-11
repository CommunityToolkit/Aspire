namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents the PostgreSQL module for Supabase.
/// </summary>
public class SupabasePostgresResource : ContainerResource,  IResourceWithParent<SupabaseResource>, IResourceWithConnectionString
{
    internal const string EndpointName = "postgres";

    private const string DefaultUserName = "postgres";

    /// <summary>
    /// Gets the endpoint for the PostgreSQL server.
    /// </summary>
    public EndpointReference Endpoint { get; }

    /// <summary>
    /// Gets or sets the parameter for the database user name.
    /// </summary>
    public ParameterResource? UserNameParameter { get; set; }

    /// <summary>
    /// Gets or sets the parameter for the database password.
    /// </summary>
    public ParameterResource PasswordParameter { get; set; }

    /// <summary>
    /// Gets or sets the parameter for the database name.
    /// </summary>
    public ParameterResource DatabaseNameParameter { get; set; } = new("database-name", _ => "postgres");

    internal ReferenceExpression UserNameReference =>
        UserNameParameter is not null
            ? ReferenceExpression.Create($"{UserNameParameter}")
            : ReferenceExpression.Create($"{DefaultUserName}");

    private ReferenceExpression ConnectionString =>
        ReferenceExpression.Create(
            $"Host={Endpoint.Property(EndpointProperty.Host)};Port={Endpoint.Property(EndpointProperty.Port)};Database={DatabaseNameParameter};Username={UserNameReference};Password={PasswordParameter}");

    /// <summary>
    /// Gets the connection string expression for the PostgreSQL database.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ConnectionString;

    /// <summary>
    /// Gets the connection string for the PostgreSQL database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string.</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        ConnectionString.GetValueAsync(cancellationToken);

    /// <summary>
    /// Initializes a new instance of the <see cref="SupabasePostgresResource"/> class.
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="userNameParameter">Optional parameter for user name.</param>
    /// <param name="passwordParameter">Parameter for the database password.</param>
    public SupabasePostgresResource(
        SupabaseResource parent,
        string name,
        ParameterResource? userNameParameter,
        ParameterResource passwordParameter)
        : base(name)
    {
        Parent = parent;
        
        UserNameParameter = userNameParameter;
        PasswordParameter = passwordParameter;
        Endpoint = new EndpointReference(this, EndpointName);
    }

    /// <summary>
    /// 
    /// </summary>
    public SupabaseResource Parent { get; }
}
