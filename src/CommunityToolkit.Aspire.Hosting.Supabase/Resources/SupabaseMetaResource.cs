namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents the Kong API gateway for Supabase.
/// </summary>
public class SupabaseMetaResource : ContainerResource/*, IResourceWithParent<SupabaseResource>*/
{
    internal const string EndpointName = "http";

    /// <summary>
    /// Gets the HTTP endpoint for the Kong API.
    /// </summary>
    public EndpointReference Endpoint { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SupabaseKongResource"/> class.
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="name">The name of the resource.</param>
    public SupabaseMetaResource(SupabaseResource parent, string name)
        : base(name)
    {
        Parent = parent;
        Endpoint = new EndpointReference(this, EndpointName);
    }

    /// <summary>
    /// The parent resource that this Kong resource belongs to.
    /// </summary>
    public SupabaseResource Parent { get; }
}
