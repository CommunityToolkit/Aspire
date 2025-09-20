using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>
/// Annotation that tracks value providers that need deferred resolution for Dapr component metadata
/// </summary>
internal sealed record DaprComponentValueProviderAnnotation(string MetadataName, string EnvironmentVariableName, IValueProvider ValueProvider) : IResourceAnnotation;