namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource for Sqlite database with a specified name and database path.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="databasePath">The path to the database directory.</param>
public class SqliteResource(string name, string databasePath) : Resource(name), IResourceWithConnectionString
{
    internal string DatabasePath { get; set; } = databasePath;

    internal string DatabaseFileName => $"{Name}.db";

    private string DatabaseFilePath => Path.Combine(DatabasePath, DatabaseFileName);

    /// <inheritdoc/>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"Data Source={DatabaseFilePath}");
}
