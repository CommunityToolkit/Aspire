using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a registered development user.
/// </summary>
public record RegisteredUser(string Email, string Password, string DisplayName);

/// <summary>
/// Represents a complete Supabase stack resource containing all sub-services.
/// This resource IS the Studio Dashboard container and serves as the visual parent
/// for all other Supabase containers in the Aspire dashboard.
/// </summary>
public sealed class SupabaseStackResource : ContainerResource, IResourceWithConnectionString
{
    /// <summary>
    /// Creates a new instance of the SupabaseStackResource.
    /// </summary>
    /// <param name="name">The name of the Supabase stack (will be the Studio container name).</param>
    public SupabaseStackResource(string name) : base(name)
    {
    }

    // --- Secrets auf Stack-Ebene ---

    /// <summary>
    /// Gets or sets the JWT secret used for token signing.
    /// </summary>
    public string JwtSecret { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the Anon Key for client-side authentication.
    /// </summary>
    public string AnonKey { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the Service Role Key for server-side authentication.
    /// </summary>
    public string ServiceRoleKey { get; internal set; } = string.Empty;

    // --- Typisierte Container Referenzen ---

    /// <summary>
    /// Gets the PostgreSQL database container resource.
    /// </summary>
    public IResourceBuilder<SupabaseDatabaseResource>? Database { get; internal set; }

    /// <summary>
    /// Gets the GoTrue authentication container resource.
    /// </summary>
    public IResourceBuilder<SupabaseAuthResource>? Auth { get; internal set; }

    /// <summary>
    /// Gets the PostgREST container resource.
    /// </summary>
    public IResourceBuilder<SupabaseRestResource>? Rest { get; internal set; }

    /// <summary>
    /// Gets the Storage API container resource.
    /// </summary>
    public IResourceBuilder<SupabaseStorageResource>? Storage { get; internal set; }

    /// <summary>
    /// Gets the Kong API Gateway container resource.
    /// </summary>
    public IResourceBuilder<SupabaseKongResource>? Kong { get; internal set; }

    /// <summary>
    /// Gets the Postgres-Meta container resource.
    /// </summary>
    public IResourceBuilder<SupabaseMetaResource>? Meta { get; internal set; }

    /// <summary>
    /// Gets the Edge Runtime container resource for Edge Functions.
    /// </summary>
    public IResourceBuilder<SupabaseEdgeRuntimeResource>? EdgeRuntime { get; internal set; }

    // --- Connection String ---

    /// <summary>
    /// Gets the connection string expression for the Supabase API (Kong endpoint URL).
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            if (Kong == null)
                throw new InvalidOperationException("Kong not configured. Ensure AddSupabase() has been called.");

            return ReferenceExpression.Create($"http://localhost:{Kong.Resource.ExternalPort.ToString()}");
        }
    }

    // --- Internal Configuration ---

    /// <summary>
    /// Reference to the distributed application builder for adding containers.
    /// </summary>
    internal IDistributedApplicationBuilder? AppBuilder { get; set; }

    /// <summary>
    /// Reference to the resource builder for this stack (used for chaining).
    /// </summary>
    internal IResourceBuilder<SupabaseStackResource>? StackBuilder { get; set; }

    /// <summary>
    /// Root directory for infrastructure files.
    /// </summary>
    internal string? InfraRootDir { get; set; }

    /// <summary>
    /// Path to the database initialization SQL scripts directory.
    /// </summary>
    internal string? InitSqlPath { get; set; }

    /// <summary>
    /// Path to the Edge Functions directory.
    /// </summary>
    internal string? EdgeFunctionsPath { get; set; }

    /// <summary>
    /// List of users to register on startup.
    /// </summary>
    internal List<RegisteredUser> RegisteredUsers { get; } = [];

    // --- Sync Configuration ---

    internal string? SyncFromProjectRef { get; set; }
    internal string? SyncServiceKey { get; set; }
    internal bool SyncSchema { get; set; } = true;
    internal bool SyncData { get; set; } = false;

    // --- Computed Properties ---

    /// <summary>
    /// Gets the Supabase API URL (Kong endpoint).
    /// </summary>
    public string GetApiUrl() =>
        Kong != null
            ? $"http://localhost:{Kong.Resource.ExternalPort}"
            : throw new InvalidOperationException("Kong not configured");

    /// <summary>
    /// Gets the Studio Dashboard URL (this resource IS the Studio).
    /// </summary>
    public string GetStudioUrl() =>
        StackBuilder != null
            ? $"http://localhost:{StudioPort}"
            : throw new InvalidOperationException("Stack not configured");

    /// <summary>
    /// Gets the PostgreSQL connection string for external tools.
    /// </summary>
    public string GetPostgresConnectionString() =>
        Database != null
            ? $"Host=localhost;Port={Database.Resource.ExternalPort};Database=postgres;Username=postgres;Password={Database.Resource.Password}"
            : throw new InvalidOperationException("Database not configured");

    /// <summary>
    /// Gets or sets the external Studio port.
    /// </summary>
    internal int StudioPort { get; set; } = 54323;
}
