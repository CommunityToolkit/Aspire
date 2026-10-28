using System.Globalization;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Floci;

/// <summary>
/// Allocates the AMQP endpoints of <see cref="FlociAzureServiceBusResource"/> children and drives their state.
/// </summary>
/// <remarks>
/// DCP does not allocate endpoints on these custom resources. Ports passed to <c>WithServiceBus</c> are
/// allocated before startup. Ports left unset are passed to floci-az as <c>0</c>, so Docker assigns them
/// atomically when it starts the Artemis sidecar. A port selected in advance and released before Docker binds
/// it can be taken by another process in the meantime. The assigned ports are read from floci-az's namespace
/// API once the emulator is ready, and the child's endpoints are allocated then.
/// </remarks>
/// <param name="notifications">Publishes the Service Bus resources' state.</param>
/// <param name="loggers">Provides the Service Bus resources' loggers.</param>
/// <param name="httpClientFactory">Creates the client for floci-az's namespace API.</param>
internal sealed class FlociServiceBusEndpointAllocator(
    ResourceNotificationService notifications,
    ResourceLoggerService loggers,
    IHttpClientFactory httpClientFactory) : IDistributedApplicationEventingSubscriber
{
    /// <summary>
    /// The name of the <see cref="HttpClient"/> used to query floci-az's namespace API.
    /// </summary>
    internal const string HttpClientName = "CommunityToolkit.Aspire.Hosting.Floci.ServiceBus";

    /// <summary>
    /// The namespace floci-az starts on boot when <c>FLOCI_AZ_SERVICES_SERVICE_BUS_START_ON_BOOT</c> is set.
    /// </summary>
    internal const string DefaultNamespace = "default";

    private static readonly TimeSpan s_discoveryTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        if (executionContext.IsRunMode)
        {
            eventing.Subscribe<BeforeStartEvent>((@event, ct) => AllocateConfiguredEndpointsAsync(eventing, @event, ct));
            eventing.Subscribe<ResourceReadyEvent>((@event, ct) => OnParentReadyAsync(eventing, @event, ct));
            eventing.Subscribe<ResourceStoppedEvent>(OnParentStoppedAsync);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Formats the host port floci-az binds for an endpoint: the configured port, or <c>0</c> for a
    /// port Docker assigns.
    /// </summary>
    /// <param name="endpoint">The Service Bus endpoint.</param>
    /// <returns>The value of floci-az's port setting for <paramref name="endpoint"/>.</returns>
    internal static string GetFlociPortSetting(EndpointAnnotation endpoint) =>
        (endpoint.Port ?? 0).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the published host ports from a floci-az namespace API response.
    /// </summary>
    /// <remarks>
    /// Reads <c>amqpHostPort</c> / <c>amqpsHostPort</c>, the ports Docker published on the host.
    /// <c>amqpPort</c> / <c>amqpsPort</c> are the sidecar's container ports when floci-az runs in a container.
    /// </remarks>
    /// <param name="json">The body of <c>GET /{account}-servicebus/namespaces/{namespace}</c>.</param>
    /// <returns>The AMQP and AMQPS host ports.</returns>
    /// <exception cref="DistributedApplicationException">The response does not report both host ports.</exception>
    internal static (int AmqpPort, int AmqpTlsPort) ParseNamespacePorts(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return (ReadPort(document.RootElement, "amqpHostPort"), ReadPort(document.RootElement, "amqpsHostPort"));

        int ReadPort(JsonElement root, string property) =>
            root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int port)
            && port is > 0 and <= 65535
                ? port
                : throw new DistributedApplicationException(
                    $"floci-az did not report a Service Bus host port in '{property}'. Response: {json}");
    }

    private async Task AllocateConfiguredEndpointsAsync(
        IDistributedApplicationEventing eventing,
        BeforeStartEvent @event,
        CancellationToken cancellationToken)
    {
        foreach (var resource in @event.Model.Resources.OfType<FlociAzureServiceBusResource>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var amqpEndpoint = resource.AmqpEndpoint.EndpointAnnotation;
            var amqpTlsEndpoint = resource.AmqpTlsEndpoint.EndpointAnnotation;
            if (amqpEndpoint.Port is not null && amqpEndpoint.Port == amqpTlsEndpoint.Port)
            {
                throw new DistributedApplicationException(
                    $"Service Bus resource '{resource.Name}' requires different AMQP and AMQPS host ports.");
            }

            await notifications.PublishUpdateAsync(resource, state => state with
            {
                State = KnownResourceStates.Waiting
            }).ConfigureAwait(false);

            // Configured ports are known up front, so consumers can resolve them before floci-az starts.
            bool allocated = false;
            foreach (var endpoint in new[] { amqpEndpoint, amqpTlsEndpoint })
            {
                if (endpoint.Port is int port && endpoint.AllocatedEndpoint is null)
                {
                    Allocate(endpoint, port);
                    allocated = true;
                }
            }

            if (amqpEndpoint.Port is null || amqpTlsEndpoint.Port is null)
            {
                RedirectContainerWaits(@event.Model, resource);
            }
            else if (allocated)
            {
                await PublishAllocatedAsync(eventing, resource, @event.Services, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Makes containers that wait for a Service Bus resource with Docker-assigned ports wait for its parent instead.
    /// </summary>
    /// <remarks>
    /// DCP tunnels a container's host dependencies into the container network when it creates the container, and
    /// needs their addresses at that point. Docker-assigned ports are not known then, so a container waiting for the
    /// child would fail to start. Waiting for the parent keeps the ordering: the container's connection string
    /// still resolves only once the ports are allocated.
    /// </remarks>
    /// <param name="model">The application model.</param>
    /// <param name="resource">The Service Bus resource.</param>
    internal static void RedirectContainerWaits(DistributedApplicationModel model, FlociAzureServiceBusResource resource)
    {
        foreach (var container in model.Resources.Where(candidate => candidate.IsContainer()))
        {
            foreach (var wait in container.Annotations.OfType<WaitAnnotation>().Where(wait => wait.Resource == resource).ToList())
            {
                container.Annotations.Remove(wait);
                container.Annotations.Add(new WaitAnnotation(resource.Parent, wait.WaitType, wait.ExitCode)
                {
                    WaitBehavior = wait.WaitBehavior
                });
            }
        }
    }

    private async Task OnParentReadyAsync(
        IDistributedApplicationEventing eventing,
        ResourceReadyEvent @event,
        CancellationToken cancellationToken)
    {
        if (@event.Resource is not FlociAzureContainerResource parent)
        {
            return;
        }

        foreach (var resource in GetChildren(@event.Services, parent))
        {
            try
            {
                var amqpEndpoint = resource.AmqpEndpoint.EndpointAnnotation;
                var amqpTlsEndpoint = resource.AmqpTlsEndpoint.EndpointAnnotation;
                if (amqpEndpoint.Port is null || amqpTlsEndpoint.Port is null)
                {
                    // Docker assigned these ports when floci-az started the sidecar; they change on every start.
                    var (amqpPort, amqpTlsPort) = await DiscoverPortsAsync(parent, cancellationToken).ConfigureAwait(false);
                    if (amqpEndpoint.Port is null)
                    {
                        Allocate(amqpEndpoint, amqpPort);
                    }

                    if (amqpTlsEndpoint.Port is null)
                    {
                        Allocate(amqpTlsEndpoint, amqpTlsPort);
                    }

                    await PublishAllocatedAsync(eventing, resource, @event.Services, cancellationToken).ConfigureAwait(false);
                }

                await notifications.PublishUpdateAsync(resource, state => state with
                {
                    State = KnownResourceStates.Running,
                    StartTimeStamp = DateTime.UtcNow,
                    StopTimeStamp = null
                }).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                loggers.GetLogger(resource).LogError(ex,
                    "Could not discover the Service Bus host ports of '{Resource}' from floci-az.", resource.Name);
                FailPendingEndpoints(resource, ex);
                await notifications.PublishUpdateAsync(resource, state => state with
                {
                    State = KnownResourceStates.FailedToStart
                }).ConfigureAwait(false);
            }
        }
    }

    private async Task OnParentStoppedAsync(ResourceStoppedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Resource is not FlociAzureContainerResource parent)
        {
            return;
        }

        foreach (var resource in GetChildren(@event.Services, parent))
        {
            await notifications.PublishUpdateAsync(resource, state => state with
            {
                State = KnownResourceStates.Exited,
                StopTimeStamp = DateTime.UtcNow
            }).ConfigureAwait(false);
        }
    }

    private async Task<(int AmqpPort, int AmqpTlsPort)> DiscoverPortsAsync(
        FlociAzureContainerResource parent,
        CancellationToken cancellationToken)
    {
        // floci-az serves HTTP and HTTPS on the same port, so the management call stays on plain HTTP.
        string? hostAndPort = await parent.PrimaryEndpoint.Property(EndpointProperty.HostAndPort)
            .GetValueAsync(cancellationToken).ConfigureAwait(false);
        var uri = new Uri(
            $"http://{hostAndPort}/{FlociAzureContainerResource.DefaultAccountName}-servicebus/namespaces/{DefaultNamespace}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_discoveryTimeout);
        using HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client.GetAsync(uri, timeout.Token).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new DistributedApplicationException(
                $"floci-az returned {(int)response.StatusCode} for {uri}: {body}");
        }

        return ParseNamespacePorts(body);
    }

    private static IEnumerable<FlociAzureServiceBusResource> GetChildren(
        IServiceProvider services,
        FlociAzureContainerResource parent) =>
        services.GetRequiredService<DistributedApplicationModel>().Resources
            .OfType<FlociAzureServiceBusResource>()
            .Where(resource => resource.Parent == parent);

    private static void FailPendingEndpoints(FlociAzureServiceBusResource resource, Exception exception)
    {
        // Release consumers already waiting for the ports instead of leaving them blocked.
        foreach (var endpoint in new[] { resource.AmqpEndpoint.EndpointAnnotation, resource.AmqpTlsEndpoint.EndpointAnnotation })
        {
            foreach (var snapshot in endpoint.AllAllocatedEndpoints.Where(snapshot => !snapshot.Snapshot.IsValueSet))
            {
                snapshot.Snapshot.SetException(exception);
            }
        }
    }

    private static void Allocate(EndpointAnnotation endpoint, int port)
    {
        // The sidecar publishes directly on the Docker host: host processes use localhost, and containers
        // use the runtime's host gateway with the same port.
        endpoint.TargetPort = port;
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, KnownHostNames.Localhost, port,
            EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.LocalhostNetwork);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
            KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
            new AllocatedEndpoint(endpoint, FlociAzureServiceBusResource.ContainerHost, port,
                EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
    }

    private static async Task PublishAllocatedAsync(
        IDistributedApplicationEventing eventing,
        FlociAzureServiceBusResource resource,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // Aspire raises these for DCP-managed resources only. The child is deliberately not an
        // IResourceWithParent: Aspire would then resolve its connection string before creating floci-az,
        // which blocks forever when the ports are only known after floci-az starts.
        await eventing.PublishAsync(new ResourceEndpointsAllocatedEvent(resource, services), cancellationToken)
            .ConfigureAwait(false);
        await eventing.PublishAsync(new ConnectionStringAvailableEvent(resource, services), cancellationToken)
            .ConfigureAwait(false);
    }
}
