namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Neon database.
/// </summary>
public class NeonDatabaseResource : Resource, IResourceWithConnectionString, IResourceWithParent<NeonProjectResource>
{
    /// <param name="name">The name of the resource.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="parent">The Neon project resource associated with this database.</param>
    public NeonDatabaseResource(string name, string databaseName, NeonProjectResource parent) : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        
        Parent = parent;
        DatabaseName = databaseName;
    }

    /// <summary>
    /// Gets the parent Neon project resource.
    /// </summary>
    public NeonProjectResource Parent { get; }

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// Gets the connection string expression for the Neon database.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Parent.ConnectionStringExpression};Database={DatabaseName}");
}
