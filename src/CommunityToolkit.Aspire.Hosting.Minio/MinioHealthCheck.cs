using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Minio;

internal sealed class MinioHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly Uri _minioHealthLiveUri;
    private readonly Uri _minioHealthClusterUri;
    private readonly Uri _minioHealthClusterReadUri;
    
    public MinioHealthCheck(string minioBaseUrl)
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(minioBaseUrl);
        _minioHealthLiveUri = new Uri("/minio/health/live", UriKind.Relative);
        _minioHealthClusterUri = new Uri("/minio/health/cluster", UriKind.Relative);
        _minioHealthClusterReadUri = new Uri("/minio/health/cluster/read", UriKind.Relative);
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Node Liveness Check
            var livenessResponse = await _httpClient.GetAsync(_minioHealthLiveUri, cancellationToken).ConfigureAwait(true);
            if (!livenessResponse.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy("MinIO is not responding to liveness checks");
            }

            // Cluster Write Quorum Check
            var clusterWriteResponse = await _httpClient.GetAsync(_minioHealthClusterUri, cancellationToken).ConfigureAwait(true);
            if (!clusterWriteResponse.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy("MinIO cluster does not have write quorum");
            }

            // Cluster Read Quorum Check
            var clusterReadResponse = await _httpClient.GetAsync(_minioHealthClusterReadUri, cancellationToken).ConfigureAwait(true);
            if (!clusterReadResponse.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy("MinIO cluster does not have read quorum");
            }

            return HealthCheckResult.Healthy("MinIO is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error occurred while checking MinIO health", ex);
        }
    }
}