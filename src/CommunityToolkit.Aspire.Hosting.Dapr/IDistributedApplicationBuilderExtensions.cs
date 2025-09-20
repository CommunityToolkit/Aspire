// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Publishing;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Extensions to <see cref="IDistributedApplicationBuilder"/> related to Dapr.
/// </summary>
public static partial class IDistributedApplicationBuilderExtensions
{

    /// <summary>
    /// Adds Dapr support to Aspire, including the ability to add Dapr sidecar to application resource.
    /// </summary>
    /// <param name="builder">The distributed application builder instance.</param>
    /// <param name="configure">Callback to configure dapr options.</param>
    /// <returns>The distributed application builder instance.</returns>
    public static IDistributedApplicationBuilder AddDapr(this IDistributedApplicationBuilder builder, Action<DaprOptions>? configure = null)
    {
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddLifecycleHook<DaprDistributedApplicationLifecycleHook>();

        return builder;
    }

    /// <summary>
    /// Adds a Dapr component to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder instance.</param>
    /// <param name="name">The name of the component.</param>
    /// <param name="type">The type of the component. This can be a generic "state" or "pubsub" string, to have Aspire choose an appropriate type when running or deploying.</param>
    /// <param name="options">Options for configuring the component, if any.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<IDaprComponentResource> AddDaprComponent(this IDistributedApplicationBuilder builder, [ResourceName] string name, string type, DaprComponentOptions? options = null)
    {
        var resource = new DaprComponentResource(name, type) { Options = options };
        var resourceBuilder = builder
            .AddResource(resource)
            .WithInitialState(new()
            {
                Properties = [],
                ResourceType = "DaprComponent",
                IsHidden = true,
                State = KnownResourceStates.NotStarted
            })
            .WithAnnotation(new ManifestPublishingCallbackAnnotation(context => WriteDaprComponentResourceToManifest(context, resource)));

        // Set up component lifecycle to manage state transitions
        SetupComponentLifecycle(resourceBuilder);

        return resourceBuilder;
    }

    private static void SetupComponentLifecycle(IResourceBuilder<IDaprComponentResource> componentBuilder)
    {
        // Hook into the component initialization for state management
        componentBuilder.OnInitializeResource(async (component, evt, ct) =>
        {
            try
            {
                // Update state to starting
                await evt.Notifications.PublishUpdateAsync(component, s => s with
                {
                    State = KnownResourceStates.Starting
                }).ConfigureAwait(false);

                // Publish before started event
                await evt.Eventing.PublishAsync(new BeforeResourceStartedEvent(component, evt.Services), ct).ConfigureAwait(false);

                // Update state to running
                await evt.Notifications.PublishUpdateAsync(component, s => s with
                {
                    State = KnownResourceStates.Running
                }).ConfigureAwait(false);

                evt.Logger.LogInformation("Dapr component '{ComponentName}' started successfully", component.Name);
            }
            catch (Exception ex)
            {
                evt.Logger.LogError(ex, "Failed to initialize Dapr component '{ComponentName}'", component.Name);

                // Update state to failed
                await evt.Notifications.PublishUpdateAsync(component, s => s with
                {
                    State = KnownResourceStates.FailedToStart
                }).ConfigureAwait(false);
            }
        });
    }


    /// <summary>
    /// Adds a "generic" Dapr pub-sub component to the application model. Aspire will configure an appropriate type when running or deploying.
    /// </summary>
    /// <param name="builder">The distributed application builder instance.</param>
    /// <param name="name">The name of the component.</param>
    /// <param name="options">Options for configuring the component, if any.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<IDaprComponentResource> AddDaprPubSub(this IDistributedApplicationBuilder builder, [ResourceName] string name, DaprComponentOptions? options = null)
    {
        return builder.AddDaprComponent(name, DaprConstants.BuildingBlocks.PubSub, options);
    }

    /// <summary>
    /// Adds a Dapr state store component to the application model. Aspire will configure an appropriate type when running or deploying.
    /// </summary>
    /// <param name="builder">The distributed application builder instance.</param>
    /// <param name="name">The name of the component.</param>
    /// <param name="options">Options for configuring the component, if any.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<IDaprComponentResource> AddDaprStateStore(this IDistributedApplicationBuilder builder, [ResourceName] string name, DaprComponentOptions? options = null)
    {
        return builder.AddDaprComponent(name, DaprConstants.BuildingBlocks.StateStore, options);
    }

    private static void WriteDaprComponentResourceToManifest(ManifestPublishingContext context, DaprComponentResource resource)
    {
        context.Writer.WriteString("type", "dapr.component.v0");
        context.Writer.WriteStartObject("daprComponent");

        if (resource.Options?.LocalPath is { } localPath)
        {
            context.Writer.TryWriteString("localPath", context.GetManifestRelativePath(localPath));
        }
        context.Writer.WriteString("type", resource.Type);

        context.Writer.WriteEndObject();
    }
}
