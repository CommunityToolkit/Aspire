using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource for Sqlite database with a specified name and database path.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="databasePath">The path to the database directory.</param>
/// <param name="databaseFileName">The filename of the database file. Must include extension.</param>
public class SqliteResource(string name, string databasePath, string databaseFileName) : Resource(name), IResourceWithConnectionString
{
    internal string DatabasePath { get; set; } = databasePath;

    internal string DatabaseFileName { get; set; } = databaseFileName;

    internal string DatabaseFilePath => Path.Combine(DatabasePath, DatabaseFileName);

    /// <inheritdoc/>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"Data Source={DatabaseFilePath};Cache=Shared;Mode=ReadWriteCreate;Extensions={JsonSerializer.Serialize(Extensions)}");

    private readonly List<SqliteExtensionMetadata> extensions = [];

    /// <summary>
    /// Gets the extensions to be loaded into the database.
    /// </summary>
    /// <remarks>
    /// Extensions are not loaded by the hosting integration, the information is provided for the client to load the extensions.
    /// </remarks>
    public IReadOnlyCollection<SqliteExtensionMetadata> Extensions => extensions;

    internal void AddExtension(SqliteExtensionMetadata extension) => extensions.Add(extension);
}

