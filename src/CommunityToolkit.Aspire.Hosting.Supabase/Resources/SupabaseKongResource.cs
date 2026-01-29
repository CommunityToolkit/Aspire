using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a Supabase Kong API Gateway container resource.
/// </summary>
public sealed class SupabaseKongResource : ContainerResource
{
    /// <summary>
    /// Creates a new instance of the SupabaseKongResource.
    /// </summary>
    /// <param name="name">The name of the Kong container.</param>
    public SupabaseKongResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the external port for the API gateway.
    /// </summary>
    public int ExternalPort { get; internal set; } = 8000;

    /// <summary>
    /// Gets or sets the Kong plugins to enable.
    /// </summary>
    public string[] Plugins { get; internal set; } = ["request-transformer", "cors", "key-auth", "acl", "basic-auth"];

    /// <summary>
    /// Gets or sets the reference to the parent stack.
    /// </summary>
    internal SupabaseStackResource? Stack { get; set; }
}
