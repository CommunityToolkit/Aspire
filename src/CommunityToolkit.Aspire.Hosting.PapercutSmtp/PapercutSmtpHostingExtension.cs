using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.PapercutSmtp;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Papercut SMTP to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class PapercutSmtpHostingExtension
{
    /// <summary>
    /// Adds Papercut SMTP to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="httpPort">The HTTP port number for the Papercut SMTP web console.</param>
    /// <param name="smtpPort">The SMTP port number for the Papercut SMTP container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport("addPapercutSmtp", Description = "Adds a Papercut SMTP container resource")]
    public static IResourceBuilder<PapercutSmtpContainerResource> AddPapercutSmtp(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? smtpPort = null)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
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
            .WithHttpHealthCheck("/health", endpointName: PapercutSmtpContainerResource.HttpEndpointName);

        return rb;
    }
}
