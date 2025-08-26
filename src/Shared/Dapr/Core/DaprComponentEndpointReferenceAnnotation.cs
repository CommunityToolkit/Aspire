using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>
/// Annotation that tracks endpoint references used in Dapr component metadata
/// </summary>
internal sealed record DaprComponentEndpointReferenceAnnotation(string MetadataName, string EnvironmentVariableName, EndpointReference Endpoint) : IResourceAnnotation;