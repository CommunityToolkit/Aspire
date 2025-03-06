using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
internal interface IDaprPublishingHelper
{
    ValueTask ExecuteProviderSpecificRequirements(IResource resource, DaprSidecarOptions? daprSidecarOptions);
}