using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Sqlite resources to an application builder.
/// </summary>
public static class SqliteResourceBuilderExtensions
{
    /// <summary>
    /// Adds an Sqlite resource to the application builder.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="databasePath">The optional path to the database file. If no path is provided the database is stored in a temporary location.</param>
    /// <param name="databaseFileName">The filename of the database file. Must include extension. If no file name is provided, a randomly generated file name is used.</param>
    /// <returns>A resource builder for the Sqlite resource.</returns>
    /// <remarks>The Sqlite resource is excluded from the manifest.</remarks>
    public static IResourceBuilder<SqliteResource> AddSqlite(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? databasePath = null, string? databaseFileName = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var resource = new SqliteResource(name, databasePath ?? Path.GetTempPath(), databaseFileName ?? $"{Path.GetFileName(Path.GetRandomFileName())}.db");

        builder.Eventing.Subscribe<BeforeStartEvent>((_, ct) =>
        {
            if (!File.Exists(resource.DatabaseFilePath))
            {
                Directory.CreateDirectory(resource.DatabasePath);
                File.Create(resource.DatabaseFilePath).Dispose();

                if (!OperatingSystem.IsWindows())
                {
                    // Change permissions for non-root accounts (container user account)
                    const UnixFileMode OwnershipPermissions =
                       UnixFileMode.UserRead | UnixFileMode.UserWrite |
                       UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                       UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

                    File.SetUnixFileMode(resource.DatabaseFilePath, OwnershipPermissions);
                }
            }

            return Task.CompletedTask;
        });

        var state = new CustomResourceSnapshot()
        {
            State = new(KnownResourceStates.Running, KnownResourceStateStyles.Success),
            ResourceType = "Sqlite",
            Properties = [
                new("DatabasePath", resource.DatabasePath),
                new("DatabaseFileName", resource.DatabaseFileName)
            ]
        };
        return builder.AddResource(resource)
                      .WithInitialState(state)
                      .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds an Sqlite Web resource to the resource builder, to allow access to the Sqlite database via a web interface.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="containerName">The optional name of the container.</param>
    /// <returns>A resource builder for the Sqlite resource.</returns>
    public static IResourceBuilder<SqliteResource> AddSqliteWeb(this IResourceBuilder<SqliteResource> builder, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        containerName ??= $"{builder.Resource.Name}-sqliteweb";

        var resource = new SqliteWebResource(containerName);

        var resourceBuilder = builder.ApplicationBuilder.AddResource(resource)
                                .WithImage(SqliteContainerImageTags.SqliteWebImage, SqliteContainerImageTags.SqliteWebTag)
                                .WithImageRegistry(SqliteContainerImageTags.SqliteWebRegistry)
                                .WithHttpEndpoint(targetPort: 8080, name: "http")
                                .WithEnvironment(context => context.EnvironmentVariables.Add("SQLITE_DATABASE", builder.Resource.DatabaseFileName))
                                .WithBindMount(builder.Resource.DatabasePath, "/data")
                                .WaitFor(builder)
                                .WithHttpHealthCheck("/")
                                .ExcludeFromManifest();

        return builder;
    }
}
