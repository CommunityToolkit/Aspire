// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Confluent Schema Registry container whose schemas are stored in Kafka.
/// </summary>
/// <param name="name">The resource name.</param>
[AspireExport(ExposeProperties = true)]
public class KafkaSchemaRegistryResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    /// <summary>
    /// The name of the registry HTTP endpoint.
    /// </summary>
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the endpoint used for Schema Registry REST requests.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the deferred registry hostname.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the deferred registry port.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the registry URI for the consuming resource's network.
    /// </summary>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{PrimaryEndpoint}");

    /// <summary>
    /// Gets the registry URL injected by WithReference.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => UriExpression;

    /// <inheritdoc />
    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}
