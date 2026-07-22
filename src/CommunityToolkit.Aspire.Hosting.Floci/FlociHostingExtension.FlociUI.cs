using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

public static partial class FlociHostingExtension
{
    /// <summary>
    /// Adds a <a href="https://github.com/floci-io/floci-ui">Floci UI</a> web console container
    /// for browsing the resources hosted by the Floci AWS emulator.
    /// </summary>
    /// <ats-summary>Adds a Floci UI web console container for the Floci resource</ats-summary>
    /// <example>
    /// Use in application host with a Floci resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var floci = builder.AddFlociAws("floci")
    ///   .WithFlociUI();
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <param name="builder">The Floci resource builder.</param>
    /// <param name="configureContainer">Configuration callback for the Floci UI container resource.
    /// Use this to attach additional clouds to the same UI console via <c>WithPluggedCloud</c>.</param>
    /// <param name="containerName">Optional. The name of the Floci UI container (default: <c>{floci-name}-ui</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociAwsContainerResource}"/> for further resource configuration.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<FlociAwsContainerResource> WithFlociUI(
        this IResourceBuilder<FlociAwsContainerResource> builder,
        Action<IResourceBuilder<FlociUIContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddOrConfigureFlociUI(builder, configureContainer, containerName);
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

    /// <summary>
    /// Attaches an additional Floci AWS emulator resource to an existing Floci UI console, so a
    /// single console can browse multiple clouds at once instead of creating a UI container per cloud.
    /// </summary>
    /// <param name="builder">The Floci UI resource builder (from the <c>configureContainer</c> callback of <c>WithFlociUI</c>).</param>
    /// <param name="cloud">The additional Floci AWS resource to attach.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociUIContainerResource}"/> for further configuration.</returns>
    [AspireExport("withPluggedCloudAws")]
    public static IResourceBuilder<FlociUIContainerResource> WithPluggedCloud(
        this IResourceBuilder<FlociUIContainerResource> builder,
        IResourceBuilder<FlociAwsContainerResource> cloud)
        => WithPluggedCloudCore(builder, cloud);

    /// <summary>
    /// Attaches an additional Floci Azure emulator resource to an existing Floci UI console, so a
    /// single console can browse multiple clouds at once instead of creating a UI container per cloud.
    /// </summary>
    /// <param name="builder">The Floci UI resource builder (from the <c>configureContainer</c> callback of <c>WithFlociUI</c>).</param>
    /// <param name="cloud">The additional Floci Azure resource to attach.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociUIContainerResource}"/> for further configuration.</returns>
    [AspireExport("withPluggedCloudAzure")]
    public static IResourceBuilder<FlociUIContainerResource> WithPluggedCloud(
        this IResourceBuilder<FlociUIContainerResource> builder,
        IResourceBuilder<FlociAzureContainerResource> cloud)
        => WithPluggedCloudCore(builder, cloud);

    /// <summary>
    /// Attaches an additional Floci GCP emulator resource to an existing Floci UI console, so a
    /// single console can browse multiple clouds at once instead of creating a UI container per cloud.
    /// </summary>
    /// <param name="builder">The Floci UI resource builder (from the <c>configureContainer</c> callback of <c>WithFlociUI</c>).</param>
    /// <param name="cloud">The additional Floci GCP resource to attach.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociUIContainerResource}"/> for further configuration.</returns>
    [AspireExport("withPluggedCloudGcp")]
    public static IResourceBuilder<FlociUIContainerResource> WithPluggedCloud(
        this IResourceBuilder<FlociUIContainerResource> builder,
        IResourceBuilder<FlociGcpContainerResource> cloud)
        => WithPluggedCloudCore(builder, cloud);

    private static IResourceBuilder<FlociUIContainerResource> WithPluggedCloudCore<TCloud>(
        IResourceBuilder<FlociUIContainerResource> builder,
        IResourceBuilder<TCloud> cloud)
        where TCloud : FlociContainerResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(cloud);

        FlociContainerResource cloudResource = cloud.Resource;
        return builder.WithEnvironment(context => cloudResource.ApplyUIEnvironment(context));
    }

    /// <summary>
    /// Shared implementation behind every provider's <c>WithFlociUI</c> overload: creates the
    /// single UI container resource on first call (or reconfigures the existing one on repeat
    /// calls for the same cloud resource), wiring in that cloud's env vars via <see cref="FlociContainerResource.ApplyUIEnvironment"/>.
    /// </summary>
    internal static void AddOrConfigureFlociUI<TFloci>(
        IResourceBuilder<TFloci> builder,
        Action<IResourceBuilder<FlociUIContainerResource>>? configureContainer,
        string? containerName)
        where TFloci : FlociContainerResource
    {
        // A UI instance connects to any number of Floci endpoints, so only one UI container is
        // created per Floci resource that calls WithFlociUI. Calling WithFlociUI again on the same
        // resource re-configures the existing UI container instead of adding a duplicate.
        if (builder.ApplicationBuilder.Resources.OfType<FlociUIContainerResource>()
                .FirstOrDefault(ui => ReferenceEquals(ui.Parent, builder.Resource)) is { } existingFlociUIResource)
        {
            var builderForExistingResource = builder.ApplicationBuilder.CreateResourceBuilder(existingFlociUIResource);
            configureContainer?.Invoke(builderForExistingResource);
            return;
        }

        containerName ??= $"{builder.Resource.Name}-ui";

        FlociContainerResource flociResource = builder.Resource;
        var flociUI = new FlociUIContainerResource(containerName, flociResource);

        var flociUIBuilder = builder.ApplicationBuilder.AddResource(flociUI)
            .WithImage(FlociContainerImageTags.UIImage, FlociContainerImageTags.UITag)
            .WithImageRegistry(FlociContainerImageTags.UIRegistry)
            .WithHttpEndpoint(
                targetPort: FlociUIContainerResource.UIPort,
                name: FlociUIContainerResource.PrimaryEndpointName)
            .WithEnvironment(context => flociResource.ApplyUIEnvironment(context))
            // Note: no explicit WaitFor — the UI is a child of the Floci resource
            // (IResourceWithParent), and Aspire disallows waiting on a parent.
            // Floci UI has no dedicated health endpoint; the SPA index responds 200 at the root.
            .WithHttpHealthCheck(
                path: "/",
                statusCode: 200,
                endpointName: FlociUIContainerResource.PrimaryEndpointName)
            .ExcludeFromManifest();

        configureContainer?.Invoke(flociUIBuilder);
    }
}

#pragma warning restore ASPIREATS001
