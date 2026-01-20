using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a Supabase GoTrue authentication container resource.
/// </summary>
public sealed class SupabaseAuthResource : ContainerResource
{
    /// <summary>
    /// Creates a new instance of the SupabaseAuthResource.
    /// </summary>
    /// <param name="name">The name of the auth container.</param>
    public SupabaseAuthResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the site URL for authentication redirects.
    /// </summary>
    public string SiteUrl { get; internal set; } = "http://localhost:3000";

    /// <summary>
    /// Gets or sets whether email auto-confirmation is enabled.
    /// </summary>
    public bool AutoConfirm { get; internal set; } = true;

    /// <summary>
    /// Gets or sets whether signup is disabled.
    /// </summary>
    public bool DisableSignup { get; internal set; } = false;

    /// <summary>
    /// Gets or sets whether anonymous users are enabled.
    /// </summary>
    public bool AnonymousUsersEnabled { get; internal set; } = true;

    /// <summary>
    /// Gets or sets the JWT expiration time in seconds.
    /// </summary>
    public int JwtExpiration { get; internal set; } = 3600;

    /// <summary>
    /// Gets or sets the reference to the parent stack.
    /// </summary>
    internal SupabaseStackResource? Stack { get; set; }
}
