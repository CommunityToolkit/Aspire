namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Represents the settings for the Sqlite client.
/// </summary>
public sealed class SqliteConnectionSettings
{
    /// <summary>
    /// The connection string of the PostgreSQL database to connect to.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the database health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Extensions to be loaded into the database.
    /// </summary>
    public IEnumerable<ExtensionMetadata> Extensions { get; set; } = [];
}

/// <summary>
/// Represents metadata for an extension to be loaded into a database.
/// </summary>
/// <param name="Extension">The name of the extension binary, eg: vec0</param>
/// <param name="PackageName">The name of the NuGet package. Only required if <paramref name="IsNuGetPackage"/> is <see langword="true" />.</param>
/// <param name="IsNuGetPackage">Indicates if the extension will be loaded from a NuGet package.</param>
/// <param name="ExtensionFolder">The folder for the extension. Only required if <paramref name="IsNuGetPackage"/> is <see langword="false" />.</param>
public record ExtensionMetadata(string Extension, string? PackageName, bool IsNuGetPackage, string? ExtensionFolder);