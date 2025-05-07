using Aspire.Hosting.ApplicationModel;
using System.Reflection;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for Adminer resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class AdminerBuilderExtensions
{
    /// <summary>
    /// Configures the host port that the Adminer resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for Adminer.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The resource builder for Adminer.</returns>
    public static IResourceBuilder<AdminerContainerResource> WithHostPort(this IResourceBuilder<AdminerContainerResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(AdminerContainerResource.PrimaryEndpointName, endpoint =>
        {
            endpoint.Port = port;
        });
    }

    /// <summary>
    /// Adds a Adminer container resource to the application.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <remarks>
    /// Multiple <see cref="AddAdminer(IDistributedApplicationBuilder, string, int?)"/> calls will return the same resource builder instance.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AdminerContainerResource> AddAdminer(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        if (builder.Resources.OfType<AdminerContainerResource>().SingleOrDefault() is { } existingAdminerResource)
        {
            var builderForExistingResource = builder.CreateResourceBuilder(existingAdminerResource);
            return builderForExistingResource;
        }
        else
        {
            var adminerContainer = new AdminerContainerResource(name);
            var adminerContainerBuilder = builder.AddResource(adminerContainer)
                                               .WithImage(AdminerContainerImageTags.Image, AdminerContainerImageTags.Tag)
                                               .WithImageRegistry(AdminerContainerImageTags.Registry)
                                               .WithHttpEndpoint(targetPort: 8080, port: port, name: AdminerContainerResource.PrimaryEndpointName)
                                               .WithUrlForEndpoint(AdminerContainerResource.PrimaryEndpointName, e => e.DisplayText = "Adminer Dashboard")
                                               .ExcludeFromManifest();

            adminerContainerBuilder.WithContainerFiles(
                destinationPath: "/var/www/html/plugins-enabled",
                callback: async (context, ct) =>
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream("CommunityToolkit.Aspire.Hosting.Adminer.login-servers.php") ?? throw new InvalidOperationException("Unable to load embedded resource 'login-servers.php'.");

                    using var reader = new StreamReader(stream);
                    var contents = await reader.ReadToEndAsync(ct);

                    return [
                        new ContainerFile
                        {
                            Contents = contents,
                            Name = "login-servers.php",
                        }
                    ];
                }
            );

            return adminerContainerBuilder;
        }
    }
}