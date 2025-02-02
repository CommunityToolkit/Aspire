using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a RavenDB database. This is a child resource of a <see cref="RavenDBServerResource"/>.
/// </summary>
public class RavenDBDatabaseResource(string name, string databaseName, RavenDBServerResource parent) : Resource(ThrowIfNull(name)), IResourceWithParent<RavenDBServerResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the parent RavenDB server resource associated with this database.
    /// </summary>
    public RavenDBServerResource Parent { get; } = ThrowIfNull(parent);

    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string DatabaseName { get; } = ThrowIfNull(databaseName);

    /// <summary>
    /// Gets the connection string expression for the RavenDB database, derived from the parent server's connection string.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Parent};Database={DatabaseName}");

    private static T ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        => argument ?? throw new ArgumentNullException(paramName);
}
