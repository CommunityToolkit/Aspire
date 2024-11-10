namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a RavenDB database. This is a child resource of a <see cref="RavenDBServerResource"/>.
/// </summary>
public class RavenDBDatabaseResource : Resource, IResourceWithParent<RavenDBServerResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the parent RavenDB server resource associated with this database.
    /// </summary>
    public RavenDBServerResource Parent { get; }

    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// Gets the connection string expression for the RavenDB database, derived from the parent server's connection string.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => Parent.ConnectionStringExpression;

    /// <summary>
    /// Initialize a resource that represents a RavenDB database.
    /// </summary>
    /// <param name="name">The name of the database resource.</param>
    /// <param name="databaseName">The name of the RavenDB database.</param>
    /// <param name="parent">The parent RavenDB server resource to which this database belongs.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="databaseName"/> or <paramref name="parent"/> is <c>null</c>.
    /// </exception>
    public RavenDBDatabaseResource(string name, string databaseName, RavenDBServerResource parent) : base(name)
    {
        ArgumentNullException.ThrowIfNull(databaseName);
        ArgumentNullException.ThrowIfNull(parent);

        Parent = parent;
        DatabaseName = databaseName;
    }
}
