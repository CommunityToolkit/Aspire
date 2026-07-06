using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.SeaweedFS.Client;

/// <summary>
/// Represents a health check for the SeaweedFS S3 Gateway.
/// </summary>
internal sealed class SeaweedFSS3HealthCheck(IAmazonS3 s3Client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // A lightweight operation to verify S3 connectivity without mutating data.
            await s3Client.ListBucketsAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "SeaweedFS S3 Gateway health check failed.", ex);
        }
    }
}