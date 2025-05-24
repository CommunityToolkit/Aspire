using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding and configuring Static Web Apps emulator.
/// </summary>
[Obsolete(
    message: "The SWA emulator integration is going to be removed in a future release.",
    error: false,
    DiagnosticId = "CTASPIRE003",
    UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
public static class SwaAppHostingExtension
{
    /// <summary>
    /// Adds a Static Web Apps emulator to the application.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This resource will not be included in the published manifest.</remarks>
    [Obsolete(
        message: "The SWA emulator integration is going to be removed in a future release.",
        error: false,
        DiagnosticId = "CTASPIRE003",
        UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
    public static IResourceBuilder<SwaResource> AddSwaEmulator(this IDistributedApplicationBuilder builder, [ResourceName] string name) =>
        builder.AddSwaEmulator(name, new SwaResourceOptions());

    /// <summary>
    /// Adds a Static Web Apps emulator to the application.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">The <see cref="SwaResourceOptions"/> to configure the SWA CLI.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This resource will not be included in the published manifest.</remarks>
    [Obsolete(
        message: "The SWA emulator integration is going to be removed in a future release.",
        error: false,
        DiagnosticId = "CTASPIRE003",
        UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
    public static IResourceBuilder<SwaResource> AddSwaEmulator(this IDistributedApplicationBuilder builder, [ResourceName] string name, SwaResourceOptions options)
    {
        var resource = new SwaResource(name, Environment.CurrentDirectory);
        return builder.AddResource(resource)
            .WithHttpEndpoint(isProxied: false, port: options.Port)
            .WithArgs(ctx =>
            {
                ctx.Args.Add("start");

                if (resource.TryGetAnnotationsOfType<SwaAppEndpointAnnotation>(out var appResource))
                {
                    ctx.Args.Add("--app-devserver-url");
                    ctx.Args.Add(appResource.First().Endpoint);
                }

                if (resource.TryGetAnnotationsOfType<SwaApiEndpointAnnotation>(out var apiResource))
                {
                    ctx.Args.Add("--api-devserver-url");
                    ctx.Args.Add(apiResource.First().Endpoint);
                }

                ctx.Args.Add("--port");
                ctx.Args.Add(options.Port.ToString());

                ctx.Args.Add("--devserver-timeout");
                ctx.Args.Add(options.DevServerTimeout.ToString());
            })
            .WithHttpHealthCheck("/.auth/me")
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Registers the application resource with the Static Web Apps emulator.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="appResource">The existing <see cref="IResourceBuilder{IResourceWithEndpoint}"/> to use as the <c>--app-devserver-url</c> argument.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete(
        message: "The SWA emulator integration is going to be removed in a future release.",
        error: false,
        DiagnosticId = "CTASPIRE003",
        UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
    public static IResourceBuilder<SwaResource> WithAppResource(this IResourceBuilder<SwaResource> builder, IResourceBuilder<IResourceWithEndpoints> appResource) =>
        builder.WithAnnotation<SwaAppEndpointAnnotation>(new(appResource), ResourceAnnotationMutationBehavior.Replace).WaitFor(appResource);

    /// <summary>
    /// Registers the API resource with the Static Web Apps emulator.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="apiResource">The existing <see cref="IResourceBuilder{IResourceWithEndpoint}"/> to use as the <c>--api-devserver-url</c> argument.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete(
        message: "The SWA emulator integration is going to be removed in a future release.",
        error: false,
        DiagnosticId = "CTASPIRE003",
        UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
    public static IResourceBuilder<SwaResource> WithApiResource(this IResourceBuilder<SwaResource> builder, IResourceBuilder<IResourceWithEndpoints> apiResource) =>
        builder.WithAnnotation<SwaApiEndpointAnnotation>(new(apiResource), ResourceAnnotationMutationBehavior.Replace).WaitFor(apiResource);
}
