namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a task runner resource for n8n.
/// </summary>
public class N8nTaskRunnerResource : ContainerResource, IResourceWithParent<N8nResource>
{
    /// <summary>
    /// Initializes a new N8nTaskRunnerResource with the specified name and parent.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="parent">The parent N8nResource the task runner connects to.</param>
    public N8nTaskRunnerResource(string name, N8nResource parent) : base(name)
    {
        Parent = parent;
    }

    /// <summary>
    /// Gets the parent <see cref="N8nResource"/> in the n8n resource hierarchy.
    /// </summary>
    public N8nResource Parent { get; }
}
