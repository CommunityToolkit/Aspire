using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding DuckDB resources to an application builder.
/// </summary>
public static class DuckDBResourceBuilderExtensions
{
    /// <summary>
    /// Adds a DuckDB resource to the application builder.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="databasePath">The optional path to the database file. If no path is provided the database is stored in a temporary location.</param>
    /// <param name="databaseFileName">The filename of the database file. Must include extension. If no file name is provided, a randomly generated file name is used.</param>
    /// <returns>A resource builder for the DuckDB resource.</returns>
    public static IResourceBuilder<DuckDBResource> AddDuckDB(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? databasePath = null, string? databaseFileName = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var resource = new DuckDBResource(name, databasePath ?? Path.GetTempPath(), databaseFileName ?? $"{Path.GetFileName(Path.GetRandomFileName())}.duckdb");

        builder.Eventing.Subscribe<BeforeStartEvent>((_, ct) =>
        {
            // Ensure the directory exists; DuckDB creates the database file on first connection.
            Directory.CreateDirectory(resource.DatabasePath);

            if (!OperatingSystem.IsWindows() && File.Exists(resource.DatabaseFilePath))
            {
                const UnixFileMode OwnershipPermissions =
                   UnixFileMode.UserRead | UnixFileMode.UserWrite |
                   UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                   UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

                File.SetUnixFileMode(resource.DatabaseFilePath, OwnershipPermissions);
            }

            return Task.CompletedTask;
        });

        var state = new CustomResourceSnapshot()
        {
            State = new(KnownResourceStates.Running, KnownResourceStateStyles.Success),
            ResourceType = "DuckDB",
            Properties = [
                new("DatabasePath", resource.DatabasePath),
                new("DatabaseFileName", resource.DatabaseFileName)
            ]
        };
        return builder.AddResource(resource)
                      .WithInitialState(state);
    }

    /// <summary>
    /// Configures the DuckDB resource to open the database in read-only mode.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>A resource builder for the DuckDB resource.</returns>
    public static IResourceBuilder<DuckDBResource> WithReadOnly(this IResourceBuilder<DuckDBResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.Resource.IsReadOnly = true;

        return builder;
    }
}
