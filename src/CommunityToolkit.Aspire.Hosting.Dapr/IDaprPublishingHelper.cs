using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Dapr;
/// <summary>
/// Provides methods to execute provider-specific requirements for Dapr publishing.
/// </summary>
public interface IDaprPublishingHelper
{
    /// <summary>
    /// Executes provider-specific requirements for the given distributed application model and resource.
    /// </summary>
    /// <param name="appModel">The distributed application model.</param>
    /// <param name="resource">The resource to apply requirements to.</param>
    /// <param name="daprSidecarOptions">The Dapr sidecar options, if any.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask ExecuteProviderSpecificRequirements(
        DistributedApplicationModel appModel,
        IResource resource,
        DaprSidecarOptions? daprSidecarOptions,
        CancellationToken cancellationToken);
}