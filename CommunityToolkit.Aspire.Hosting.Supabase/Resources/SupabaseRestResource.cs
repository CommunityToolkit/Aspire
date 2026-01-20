using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a Supabase PostgREST container resource.
/// </summary>
public sealed class SupabaseRestResource : ContainerResource
{
    /// <summary>
    /// Creates a new instance of the SupabaseRestResource.
    /// </summary>
    /// <param name="name">The name of the REST container.</param>
    public SupabaseRestResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the database schemas to expose.
    /// </summary>
    public string[] Schemas { get; internal set; } = ["public", "storage", "graphql_public"];

    /// <summary>
    /// Gets or sets the anonymous role name.
    /// </summary>
    public string AnonRole { get; internal set; } = "anon";

    /// <summary>
    /// Gets or sets the reference to the parent stack.
    /// </summary>
    internal SupabaseStackResource? Stack { get; set; }
}
