using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a Supabase PostgreSQL database container resource.
/// </summary>
public sealed class SupabaseDatabaseResource : ContainerResource
{
    /// <summary>
    /// Creates a new instance of the SupabaseDatabaseResource.
    /// </summary>
    /// <param name="name">The name of the database container.</param>
    public SupabaseDatabaseResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the database password.
    /// </summary>
    public string Password { get; internal set; } = "postgres-insecure-dev-password";

    /// <summary>
    /// Gets or sets the external port for PostgreSQL connections.
    /// </summary>
    public int ExternalPort { get; internal set; } = 54322;

    /// <summary>
    /// Gets or sets the reference to the parent stack.
    /// </summary>
    internal SupabaseStackResource? Stack { get; set; }
}
