namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a meta resource for all Supabases containers.
/// </summary>
public class SupabaseResource : ContainerResource, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string DatabaseEndpointName = "postgresql";

    /// <param name="name">The name of the resource.</param>
    public SupabaseResource(string name) : base(name)
    {
    }

    private EndpointReference? _primaryEndpoint;
    private EndpointReference? _databaseEndpoint;

    /// <summary>
    /// Gets the primary endpoint for Supabase. This endpoint is used for API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the database endpoint for Supabase PostgreSQL database.
    /// </summary>
    public EndpointReference DatabaseEndpoint => _databaseEndpoint ??= new EndpointReference(this, DatabaseEndpointName);
    
    private const string DefaultDashboardUserName = "admin";
    
    /// <summary>
    /// 
    /// </summary>
    public ParameterResource? DashboardUserNameParameter { get; set; }
    internal ReferenceExpression DashboardUserNameReference =>
        DashboardUserNameParameter is not null ?
            ReferenceExpression.Create($"{DashboardUserNameParameter}") :
            ReferenceExpression.Create($"{DefaultDashboardUserName}");
    
    /// <summary>
    /// Gets the parameter that contains the Supabase database password.
    /// </summary>
    public ParameterResource DashboardPasswordParameter { get; set; }= null!;
    
    private const string DefaultDatabaseUserName = "postgres";
   

    /// <summary>
    /// 
    /// </summary>
    public ParameterResource? DatabaseUserNameParameter { get; set; }
    internal ReferenceExpression DatabaseUserNameReference =>
        DatabaseUserNameParameter is not null ?
            ReferenceExpression.Create($"{DatabaseUserNameParameter}") :
            ReferenceExpression.Create($"{DefaultDatabaseUserName}");
    /// <summary>
    /// 
    /// </summary>
    public ParameterResource DatabasePasswordParameter { get; set; } = null!;
    
    /// <summary>
    /// 
    /// </summary>
    public ParameterResource DatabaseNameParameter { get; set; } = new("database-name", _ => "postgres");
    
    /// <summary>
    /// 
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            if (this.TryGetLastAnnotation(out ConnectionStringRedirectAnnotation? connectionStringAnnotation))
            {
                return connectionStringAnnotation.Resource.ConnectionStringExpression;
            }

            return ConnectionString;
        }
    }
    
    /// <summary>
    /// Gets the connection string for the PostgreSQL server.
    /// </summary>
    /// <param name="cancellationToken"> A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the PostgreSQL server in the form "Host=host;Port=port;Username=postgres;Password=password".</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation(out ConnectionStringRedirectAnnotation? connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the connection string expression for the Supabase PostgreSQL database.
    /// </summary>
    private ReferenceExpression ConnectionString =>
        ReferenceExpression.Create($"Host={DatabaseEndpoint.Property(EndpointProperty.Host)};Port={DatabaseEndpoint.Property(EndpointProperty.Port)};Database={DatabaseNameParameter};Username={DatabaseUserNameReference};Password={DatabasePasswordParameter}");

}