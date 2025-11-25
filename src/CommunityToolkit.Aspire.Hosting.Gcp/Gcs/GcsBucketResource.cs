using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Gcp.Gcs;

public class GcsBucketResource(string resourceName, GcsResource gcs, ReferenceExpression? bucketName = null): Resource(resourceName), IResourceWithParent<GcsResource>, IResourceWithWaitSupport
{
    public ReferenceExpression BucketNameParameter { get; } = bucketName ?? ReferenceExpression.Create($"{resourceName}");

    public GcsResource Parent { get; } = gcs;
    
    // TODO: add connection string properties with different formats
}
