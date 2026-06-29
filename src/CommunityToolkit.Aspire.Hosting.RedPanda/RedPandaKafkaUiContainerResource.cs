namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Kafka UI container (the <c>kafbat/kafka-ui</c> image), a web UI for managing
/// and inspecting one or more Kafka API compatible brokers such as Redpanda.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class RedPandaKafkaUiContainerResource(string name) : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";
    internal const int HttpPort = 8080;
}
