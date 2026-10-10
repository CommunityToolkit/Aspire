using System.Net;
using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace CommunityToolkit.Aspire.Hosting.Floci;

internal sealed class FlociServiceBusEndpointAllocator : IDistributedApplicationEventingSubscriber
{
    // Random host ports are taken from below every OS ephemeral range (Linux starts at 32768,
    // Windows and macOS at 49152). An OS-assigned port is released before Docker binds it, and DCP
    // can hand it to a project proxy in the meantime: on Windows the proxy's loopback listener then
    // shadows Docker's wildcard bind, and on Linux Docker's bind fails.
    private const int MinSelectedPort = 20000;
    private const int MaxSelectedPortExclusive = 32768;

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
        // Ports configured anywhere in the app model are bound later, so probing cannot see them.
        var reservedPorts = @event.Model.Resources
            .SelectMany(resource => resource.Annotations.OfType<EndpointAnnotation>())
            .Select(endpoint => endpoint.Port)
            .OfType<int>()
            .ToHashSet();
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

                    endpoint.Port ??= SelectPort(listeners, reservedPorts);
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

    internal static int SelectPort(List<TcpListener> listeners, HashSet<int> reservedPorts)
    {
        // Start at a random offset so concurrent AppHosts spread out, then scan the whole range so a
        // free port is always found when one exists. Reserved ports are skipped without probing.
        int rangeSize = MaxSelectedPortExclusive - MinSelectedPort;
        int offset = Random.Shared.Next(rangeSize);
        for (int i = 0; i < rangeSize; i++)
        {
            int port = MinSelectedPort + (offset + i) % rangeSize;
            if (reservedPorts.Contains(port))
            {
                continue;
            }

            // Match Docker's bind address, as in the k3s allocator, and also probe IPv4 and IPv6 loopback, where
            // another process's listener would shadow Docker's wildcard bind. This probes availability;
            // it cannot reserve the port through Docker startup, which needs the socket released.
            if (!IsAvailable(IPAddress.Loopback, port)
                || (Socket.OSSupportsIPv6 && !IsAvailable(IPAddress.IPv6Loopback, port)))
            {
                continue;
            }

            var listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start();
            }
            catch (SocketException)
            {
                listener.Dispose();
                continue;
            }

            listeners.Add(listener);
            reservedPorts.Add(port);
            return port;
        }

        throw new DistributedApplicationException(
            $"No free Service Bus host port was found in {MinSelectedPort}-{MaxSelectedPortExclusive - 1}.");
    }

    private static bool IsAvailable(IPAddress address, int port)
    {
        using var listener = new TcpListener(address, port);
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
