using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
internal interface IDaprPublishingHelper
{
    ValueTask ExecuteProviderSpecificRequirements(
        DistributedApplicationModel appModel, 
        IResource resource, 
        DaprSidecarOptions? daprSidecarOptions,
        CancellationToken cancellationToken);
}