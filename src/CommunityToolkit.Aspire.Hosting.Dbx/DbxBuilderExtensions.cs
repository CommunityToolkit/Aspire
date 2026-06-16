using Aspire.Hosting.ApplicationModel;
using System.Text.Json;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for dbx resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DbxBuilderExtensions
{
    private const string DBX_STATIC_DIR = "/app/static";
    private const string DBX_DATA_DIR = "/app/data";
        
    /// <summary>
    /// Configures the host port that the dbx resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for dbx.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The resource builder for dbx.</returns>
    [AspireExport]
    public static IResourceBuilder<DbxContainerResource> WithHostPort(this IResourceBuilder<DbxContainerResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(DbxContainerResource.PrimaryEndpointName, endpoint =>
        {
            endpoint.Port = port;
        });
    }

    /// <summary>
    /// Adds a dbx container resource to the application.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency. Optional; defaults to <c>dbx</c>.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <remarks>
    /// Multiple <see cref="AddDbx(IDistributedApplicationBuilder, string, int?)"/> calls will return the same resource builder instance.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<DbxContainerResource> AddDbx(this IDistributedApplicationBuilder builder, [ResourceName] string name = "dbx", int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        if (builder.Resources.OfType<DbxContainerResource>().SingleOrDefault() is { } existingDbxResource)
        {
            var builderForExistingResource = builder.CreateResourceBuilder(existingDbxResource);
            return builderForExistingResource;
        }
        
        var dbxContainer = new DbxContainerResource(name);
        var dbxContainerBuilder = builder.AddResource(dbxContainer)
                                           .WithImage(DbxContainerImageTags.Image, DbxContainerImageTags.Tag)
                                           .WithImageRegistry(DbxContainerImageTags.Registry)
                                           .WithHttpEndpoint(targetPort: 4224, port: port, name: DbxContainerResource.PrimaryEndpointName)
                                           .WithUrlForEndpoint(DbxContainerResource.PrimaryEndpointName, e => e.DisplayText = "dbx Dashboard")
                                           .WithIconName("WindowDatabase")
                                           .WithEnvironment("DBX_DISABLE_PASSWORD", "true")
                                           .ExcludeFromManifest();

        dbxContainerBuilder.WithContainerFiles(
            destinationPath: DBX_DATA_DIR,
            callback: (context, _) =>
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower
                };
                var connectionsContents = JsonSerializer.Serialize(dbxContainer.Connections, options);

                var secrets = dbxContainer.Connections
                    .ToDictionary(connection => $"connection:{connection.Id}:password", connection => connection.Password);
                var secretsContents = JsonSerializer.Serialize(secrets);
        
                IEnumerable<ContainerFileSystemItem> files = [
                    new ContainerFile
                    {
                        Contents = connectionsContents, 
                        Name = "connections.json",
                    },
                    new ContainerFile
                    {
                        Contents = secretsContents, 
                        Name = "secrets.json",
                    }
                ];
                
                return Task.FromResult(files);
            }
        );
        
        return dbxContainerBuilder;
    }
}

#pragma warning restore ASPIREATS001
