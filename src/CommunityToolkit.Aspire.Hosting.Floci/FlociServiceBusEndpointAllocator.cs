using System.Net;
using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace CommunityToolkit.Aspire.Hosting.Floci;

internal sealed class FlociServiceBusEndpointAllocator : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        if (executionContext.IsRunMode)
        {
            eventing.Subscribe<BeforeStartEvent>((@event, ct) => AllocateEndpointsAsync(eventing, @event, ct));
        }

        return Task.CompletedTask;
    }

    private static async Task AllocateEndpointsAsync(
        IDistributedApplicationEventing eventing,
        BeforeStartEvent @event,
        CancellationToken cancellationToken)
    {
        // DCP does not allocate endpoints on these custom resources. Allocate before the
        // parent's environment is evaluated, otherwise resolving its child's ports blocks startup.
        var listeners = new List<TcpListener>();
        var allocatedResources = new List<FlociAzureServiceBusResource>();
        try
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

                bool allocated = false;
                foreach (var endpoint in new[] { amqpEndpoint, amqpTlsEndpoint })
                {
                    if (endpoint.AllocatedEndpoint is not null)
                    {
                        continue;
                    }

                    endpoint.Port ??= SelectPort(listeners);
                    endpoint.TargetPort = endpoint.Port;
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, KnownHostNames.Localhost, endpoint.Port.Value,
                        EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.LocalhostNetwork);
                    endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
                        KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
                        new AllocatedEndpoint(endpoint, FlociAzureServiceBusResource.ContainerHost, endpoint.Port.Value,
                            EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
                    allocated = true;
                }

                if (allocated)
                {
                    allocatedResources.Add(resource);
                }
            }
        }
        finally
        {
            // Keep all probes bound until every port is selected, then release them for Docker.
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
        }

        foreach (var resource in allocatedResources)
        {
            await eventing.PublishAsync(
                new ResourceEndpointsAllocatedEvent(resource, @event.Services), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static int SelectPort(List<TcpListener> listeners)
    {
        // Match Docker's bind address, as in the k3s allocator. This probes availability;
        // it cannot reserve the port through Docker startup, which needs the socket released.
        var listener = new TcpListener(IPAddress.Any, 0);
        listeners.Add(listener);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
