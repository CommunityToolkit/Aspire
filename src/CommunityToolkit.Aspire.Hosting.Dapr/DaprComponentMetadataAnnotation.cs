using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
internal sealed record DaprComponentConfigurationAnnotation(Func<DaprComponentSchema, CancellationToken, Task> Configure) : IResourceAnnotation;
