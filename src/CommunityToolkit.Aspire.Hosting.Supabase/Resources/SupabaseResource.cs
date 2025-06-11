namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a meta resource for all Supabases containers.
/// </summary>
public class SupabaseResource : ContainerResource, IResourceWithConnectionString
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SupabaseResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="postgresUserParam">Optional parameter for database user name.</param>
    /// <param name="postgresPasswordParam">Parameter for the database password.</param>
    /// <param name="dashboardUserParam">Optional parameter for dashboard user name.</param>
    /// <param name="dashboardPasswordParam">Parameter for the dashboard password.</param>
    public SupabaseResource(
        string name,
        ParameterResource? postgresUserParam,
        ParameterResource postgresPasswordParam,
        ParameterResource? dashboardUserParam,
        ParameterResource dashboardPasswordParam)
        : base(name)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public ReferenceExpression ConnectionStringExpression { get; } = null!;
    
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
}