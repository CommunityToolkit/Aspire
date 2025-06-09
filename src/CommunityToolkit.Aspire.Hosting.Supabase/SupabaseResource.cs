namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a meta resource for all Supabases containers.
/// </summary>
public class SupabaseResource : ContainerResource, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string DatabaseEndpointName = "postgresql";

    /// <param name="name">The name of the resource.</param>
    /// <param name="password">A parameter that contains the Supabase database password.</param>
    public SupabaseResource(string name, ParameterResource password) : base(name)
    {
        ArgumentNullException.ThrowIfNull(password);
        PasswordParameter = password;
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

    /// <summary>
    /// Gets the parameter that contains the Supabase database password.
    /// </summary>
    public ParameterResource PasswordParameter { get; }

    /// <summary>
    /// Gets the connection string expression for the Supabase PostgreSQL database.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Host={DatabaseEndpoint.Property(EndpointProperty.Host)};Port={DatabaseEndpoint.Property(EndpointProperty.Port)};Database=postgres;Username=postgres;Password={PasswordParameter}");
}