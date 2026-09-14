namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a worker resource for n8n.
/// </summary>
public class N8nWorkerResource : N8nResource, IResourceWithParent<N8nResource>
{
    /// <summary>
    /// Initializes a new N8nWorkerResource with the specified name and parent.
    /// </summary>
    /// <remarks>Sets the Parent property and forwards the parent's EncryptionKeyParameter to the base
    /// constructor.</remarks>
    /// <param name="name">The resource name.</param>
    /// <param name="parent">The parent N8nResource whose scope and EncryptionKeyParameter are used.</param>
    public N8nWorkerResource(string name, N8nResource parent) : base(name, parent.EncryptionKeyParameter)
    {
        Parent = parent;
    }

    /// <summary>
    /// Gets the parent <see cref="N8nResource"/> in the n8n resource hierarchy.
    /// </summary>
    public N8nResource Parent { get; }
}
