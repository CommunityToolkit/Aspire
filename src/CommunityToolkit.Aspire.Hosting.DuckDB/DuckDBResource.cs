namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource for a DuckDB database with a specified name and database path.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="databasePath">The path to the database directory.</param>
/// <param name="databaseFileName">The filename of the database file. Must include extension.</param>
public class DuckDBResource(string name, string databasePath, string databaseFileName) : Resource(name), IResourceWithConnectionString
{
    internal string DatabasePath { get; set; } = databasePath;

    internal string DatabaseFileName { get; set; } = databaseFileName;

    internal string DatabaseFilePath => Path.Combine(DatabasePath, DatabaseFileName);

    internal bool IsReadOnly { get; set; }

    /// <inheritdoc/>
    public ReferenceExpression ConnectionStringExpression =>
        IsReadOnly
            ? ReferenceExpression.Create($"DataSource={DatabaseFilePath};Access Mode=ReadOnly")
            : ReferenceExpression.Create($"DataSource={DatabaseFilePath}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("DataSource", ReferenceExpression.Create($"{DatabaseFilePath}"));
    }
}
