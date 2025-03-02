using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.MailPit;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding MailPit to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class MailPitHostingExtension
{
    /// <summary>
    /// Adds a MailPit container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the MailPit resource will be added.</param>
    /// <param name="name">The name of the MailPit container resource.</param>
    /// <param name="httpPort">Optional. The HTTP port on which MailPit will listen.</param>
    /// <param name="smtpPort">Optional. The SMTP port on which MailPit will listen.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{MailPitContainerResource}"/> for further resource configuration.</returns>
    public static IResourceBuilder<MailPitContainerResource> AddMailPit(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? smtpPort = null)
    {
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));
        MailPitContainerResource resource = new(name);

        IResourceBuilder<MailPitContainerResource> rb = builder.AddResource(resource)
            .WithImage(MailPitContainerImageTags.Image)
            .WithImageTag(MailPitContainerImageTags.Tag)
            .WithImageRegistry(MailPitContainerImageTags.Registry)
            .WithEndpoint(
                targetPort: MailPitContainerResource.SmtpEndpointPort,
                port: smtpPort,
                name: MailPitContainerResource.SmtpEndpointName,
                scheme: "smtp")
            .WithHttpEndpoint(
                targetPort: MailPitContainerResource.HttpEndpointPort,
                port: httpPort,
                name: MailPitContainerResource.HttpEndpointName)
            .WithHttpHealthCheck(
                path: "/livez",
                statusCode: 200,
                endpointName: MailPitContainerResource.HttpEndpointName)
            .WithHttpHealthCheck(
                path: "/readyz",
                statusCode: 200,
                endpointName: MailPitContainerResource.HttpEndpointName);

        return rb;
    }

    /// <summary>
    /// Configures a data volume for the MailPit container resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="name">The name of the data volume to be mounted.</param>
    /// <param name="isReadOnly">A boolean indicating whether the volume should be mounted as read-only.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    public static IResourceBuilder<MailPitContainerResource> WithDataVolume(this IResourceBuilder<MailPitContainerResource> builder, string name,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables[MailPitContainerResource.DatabaseEnvVar] = "/data/mailpit.db";
        }).WithVolume(name, "/data", isReadOnly);
    }

    /// <summary>
    /// Configures a bind mount for the data directory of the MailPit container resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{MailPitContainerResource}"/> to configure the bind mount on.</param>
    /// <param name="source">The source path on the host system to bind to the container.</param>
    /// <param name="isReadOnly">A value indicating whether the bind mount should be read-only. Default is false.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{MailPitContainerResource}"/> with the configured bind mount.</returns>
    public static IResourceBuilder<MailPitContainerResource> WithDataBindMount(this IResourceBuilder<MailPitContainerResource> builder, string source,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables[MailPitContainerResource.DatabaseEnvVar] = "/data/mailpit.db";
        }).WithBindMount(source, "/data", isReadOnly);
    }
}
