using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

internal class DefaultDaprPublishingHelper : IDaprPublishingHelper
{
    public ValueTask ExecuteProviderSpecificRequirements(IResource resource, DaprSidecarOptions? daprSidecarOptions) => ValueTask.CompletedTask;
}
