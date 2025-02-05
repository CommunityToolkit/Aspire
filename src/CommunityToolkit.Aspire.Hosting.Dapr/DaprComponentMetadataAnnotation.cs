using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
internal sealed record DaprComponentMetadataAnnotation(string Name, object Value) : IResourceAnnotation;
