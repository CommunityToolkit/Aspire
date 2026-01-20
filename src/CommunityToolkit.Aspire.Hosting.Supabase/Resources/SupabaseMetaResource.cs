using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a Supabase Postgres-Meta container resource.
/// </summary>
public sealed class SupabaseMetaResource : ContainerResource
{
    /// <summary>
    /// Creates a new instance of the SupabaseMetaResource.
    /// </summary>
    /// <param name="name">The name of the meta container.</param>
    public SupabaseMetaResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the internal port for the meta service.
    /// </summary>
    public int Port { get; internal set; } = 8080;

    /// <summary>
    /// Gets or sets the reference to the parent stack.
    /// </summary>
    internal SupabaseStackResource? Stack { get; set; }
}
