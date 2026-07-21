using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

public static partial class FlociHostingExtension
{
    /// <summary>
    /// Adds a <a href="https://github.com/floci-io/floci-ui">Floci UI</a> web console container
    /// for browsing the resources hosted by the Floci emulator.
    /// </summary>
    /// <ats-summary>Adds a Floci UI web console container for the Floci resource</ats-summary>
    /// <example>
    /// Use in application host with a Floci resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var floci = builder.AddFloci("floci")
    ///   .WithFlociUI();
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <param name="builder">The Floci resource builder.</param>
    /// <param name="configureContainer">Configuration callback for the Floci UI container resource.</param>
    /// <param name="containerName">Optional. The name of the Floci UI container (default: <c>{floci-name}-ui</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociContainerResource}"/> for further resource configuration.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<FlociContainerResource> WithFlociUI(
        this IResourceBuilder<FlociContainerResource> builder,
        Action<IResourceBuilder<FlociUIContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // A UI instance connects to a single Floci endpoint, so each Floci resource gets its
        // own UI container. Calling WithFlociUI again on the same resource re-configures the
        // existing UI container instead of adding a duplicate.
        if (builder.ApplicationBuilder.Resources.OfType<FlociUIContainerResource>()
                .FirstOrDefault(ui => ReferenceEquals(ui.Parent, builder.Resource)) is { } existingFlociUIResource)
        {
            var builderForExistingResource = builder.ApplicationBuilder.CreateResourceBuilder(existingFlociUIResource);
            configureContainer?.Invoke(builderForExistingResource);
            return builder;
        }

        containerName ??= $"{builder.Resource.Name}-ui";

        var flociResource = builder.Resource;
        var flociUI = new FlociUIContainerResource(containerName, flociResource);

        var flociUIBuilder = builder.ApplicationBuilder.AddResource(flociUI)
            .WithImage(FlociContainerImageTags.UIImage, FlociContainerImageTags.UITag)
            .WithImageRegistry(FlociContainerImageTags.UIRegistry)
            .WithHttpEndpoint(
                targetPort: FlociUIContainerResource.UIPort,
                name: FlociUIContainerResource.PrimaryEndpointName)
            .WithEnvironment(context =>
            {
                // Floci serves HTTP and HTTPS on the same port (4566), so the http:// endpoint
                // URL stays valid even when TLS is enabled on the Floci resource.
                context.EnvironmentVariables[FlociUIContainerResource.EndpointEnvVar] =
                    ReferenceExpression.Create($"{flociResource.PrimaryEndpoint}");
                context.EnvironmentVariables[FlociUIContainerResource.RegionEnvVar] = flociResource.DefaultRegion;
                context.EnvironmentVariables[FlociUIContainerResource.AccessKeyIdEnvVar] = "test";
                context.EnvironmentVariables[FlociUIContainerResource.SecretAccessKeyEnvVar] = "test";
                context.EnvironmentVariables[FlociUIContainerResource.DefaultAccountIdEnvVar] = flociResource.DefaultAccountId;
            })
            // Note: no explicit WaitFor — the UI is a child of the Floci resource
            // (IResourceWithParent), and Aspire disallows waiting on a parent.
            // Floci UI has no dedicated health endpoint; the SPA index responds 200 at the root.
            .WithHttpHealthCheck(
                path: "/",
                statusCode: 200,
                endpointName: FlociUIContainerResource.PrimaryEndpointName)
            .ExcludeFromManifest();

        configureContainer?.Invoke(flociUIBuilder);

        return builder;
    }

    /// <summary>
    /// Configures the host port that the Floci UI resource is exposed on instead of using a randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for Floci UI.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used a random port will be assigned.</param>
    /// <returns>The resource builder for Floci UI.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociUIContainerResource> WithHostPort(
        this IResourceBuilder<FlociUIContainerResource> builder,
        int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(FlociUIContainerResource.PrimaryEndpointName, endpoint =>
        {
            endpoint.Port = port;
        });
    }
}

#pragma warning restore ASPIREATS001
