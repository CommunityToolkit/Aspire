using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a Supabase Storage API container resource.
/// </summary>
public sealed class SupabaseStorageResource : ContainerResource
{
    /// <summary>
    /// Creates a new instance of the SupabaseStorageResource.
    /// </summary>
    /// <param name="name">The name of the storage container.</param>
    public SupabaseStorageResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the maximum file size limit in bytes.
    /// </summary>
    public long FileSizeLimit { get; internal set; } = 52428800; // 50MB

    /// <summary>
    /// Gets or sets the storage backend type.
    /// </summary>
    public string Backend { get; internal set; } = "file";

    /// <summary>
    /// Gets or sets whether image transformation is enabled.
    /// </summary>
    public bool EnableImageTransformation { get; internal set; } = true;

    /// <summary>
    /// Gets or sets the reference to the parent stack.
    /// </summary>
    internal SupabaseStackResource? Stack { get; set; }
}
