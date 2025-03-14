using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

internal class DefaultDaprPublishingHelper : IDaprPublishingHelper
{
    public ValueTask ExecuteProviderSpecificRequirements(
        DistributedApplicationModel appModel, 
        IResource resource, 
        DaprSidecarOptions? daprSidecarOptions,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
