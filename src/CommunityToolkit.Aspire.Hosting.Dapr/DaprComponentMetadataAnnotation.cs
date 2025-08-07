using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
internal sealed record DaprComponentConfigurationAnnotation(Func<DaprComponentSchema, Task> Configure) : IResourceAnnotation;
