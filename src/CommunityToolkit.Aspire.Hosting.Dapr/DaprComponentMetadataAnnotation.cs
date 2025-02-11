using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
internal sealed record DaprComponentConfigurationAnnotation(Action<DaprComponentSchema> Configure) : IResourceAnnotation;
