namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents the Supabase Studio module.
/// </summary>
public class SupabaseStudioResource : ContainerResource, IResourceWithParent<SupabaseResource>
{
    internal const string EndpointName = "studio";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SupabaseStudioResource"/> class.
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="name">The name of the resource.</param>
    public SupabaseStudioResource(SupabaseResource parent,string name)
        : base(name)
    {
        Parent = parent;
    }

    /// <summary>
    /// 
    /// </summary>
    public SupabaseResource Parent { get; }
}
