namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Redpanda Console container, a web UI for managing and inspecting
/// one or more Redpanda (or Kafka) brokers.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class RedPandaConsoleContainerResource(string name) : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";
    internal const int HttpPort = 8080;
}
