using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Ollama;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding DataApiBuilder api to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class PapercutSmtpHostingExtension
{
    /// <summary>
    /// Adds Papercut SMTP to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <remarks>
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<PapercutSmtpContainerResource> AddPapercutSmtp(this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        return builder.AddPapercutSmtp(name, null);
    }

    /// <summary>
    /// Adds Papercut SMTP to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="httpPort">The HTTP portnumber for the web-console to the Papercut SMTP container.</param>
    /// <param name="smtpPort">The SMTP portnumber for the Papercut SMTP Conteainer</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<PapercutSmtpContainerResource> AddPapercutSmtp(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? smtpPort = null)
    {
        ArgumentNullException.ThrowIfNull("Service name must be specified.", nameof(name));
        PapercutSmtpContainerResource resource = new(name);

        IResourceBuilder<PapercutSmtpContainerResource> rb = builder.AddResource(resource)
            .WithImage(PapercutSmtpContainerImageTags.Image)
            .WithImageTag(PapercutSmtpContainerImageTags.Tag)
            .WithImageRegistry(PapercutSmtpContainerImageTags.Registry)
            .WithEndpoint(targetPort: PapercutSmtpContainerResource.SmtpEndpointPort,
                port: smtpPort,
                name: PapercutSmtpContainerResource.SmtpEndpointName,
                scheme: "smtp")
            .WithHttpEndpoint(targetPort: PapercutSmtpContainerResource.HttpEndpointPort,
                port: httpPort,
                name: PapercutSmtpContainerResource.HttpEndpointName)
            .WithHttpHealthCheck("", httpPort, PapercutSmtpContainerResource.HttpEndpointName);

        return rb;
    }
}
