using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

public static partial class FlociHostingExtension
{
    /// <summary>
    /// Wires the emulator's TLS listener to Aspire's certificate plumbing. Called from
    /// <c>AddFlociAws</c>/<c>AddFlociAzure</c> so that configuring a certificate the Aspire way —
    /// <c>WithHttpsDeveloperCertificate()</c>, <c>WithHttpsCertificate(...)</c> — is all a consumer
    /// needs: Aspire provisions and mounts the key pair and hands us the container-side paths, and
    /// this integration maps them onto the image's own settings. There is no Floci-specific TLS API.
    /// </summary>
    /// <remarks>
    /// Unlike resources that are HTTPS-first, an emulator's normal mode is plain HTTP (LocalStack
    /// parity), so the scheme is only switched when a certificate has been asked for explicitly.
    /// Merely having a trusted ASP.NET Core development certificate on the machine does not flip an
    /// existing AppHost to <c>https</c>.
    /// </remarks>
    internal static void ConfigureTlsCore<TFloci>(IDistributedApplicationBuilder builder, IResourceBuilder<TFloci> flociBuilder)
        where TFloci : FlociContainerResource
    {
        FlociTlsEnvVars envVars = flociBuilder.Resource.TlsEnvVars
            ?? throw new InvalidOperationException($"The image behind '{flociBuilder.Resource.Name}' does not support TLS.");

        // Invoked only once Aspire has an actual certificate for this resource, so it doubles as the
        // gate: no certificate configured means the emulator is never switched into TLS mode.
        flociBuilder.WithHttpsCertificateConfiguration(context =>
        {
            context.EnvironmentVariables[envVars.Enabled] = "true";
            context.EnvironmentVariables[envVars.SelfSigned] = "false";
            context.EnvironmentVariables[envVars.CertPath] = context.CertificatePath;
            context.EnvironmentVariables[envVars.KeyPath] = context.KeyPath;
            return Task.CompletedTask;
        });

        // Floci binds one port for both protocols, so there is no second endpoint to add — the
        // existing one is re-scheme'd. Deferred to BeforeStartEvent because whether a certificate
        // will be used is only decidable once IDeveloperCertificateService can be resolved.
        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            TFloci resource = flociBuilder.Resource;

            if (!resource.TryGetLastAnnotation<HttpsCertificateAnnotation>(out var certificate))
            {
                return Task.CompletedTask;
            }

            bool useHttps;
            if (certificate.Certificate is not null)
            {
                useHttps = true;
            }
            else if (certificate.UseDeveloperCertificate is bool explicitChoice)
            {
                useHttps = explicitChoice;
            }
            else
            {
                useHttps = evt.Services.GetRequiredService<IDeveloperCertificateService>().UseForHttps;
            }

            if (useHttps)
            {
                flociBuilder.WithEndpoint(resource.EndpointName, endpoint => endpoint.UriScheme = "https");
            }

            return Task.CompletedTask;
        });
    }
}
