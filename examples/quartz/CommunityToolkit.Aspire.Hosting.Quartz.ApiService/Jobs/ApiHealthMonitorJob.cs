using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// API health monitor job - demonstrates HTTP calls and retry logic
/// </summary>
public class ApiHealthMonitorJob : IJob
{
    private readonly ILogger<ApiHealthMonitorJob> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public const string Name = nameof(ApiHealthMonitorJob);
    public const string Group = "monitoring";

    public ApiHealthMonitorJob(
        ILogger<ApiHealthMonitorJob> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var apiUrl = data.GetString("apiUrl") ?? "https://api.example.com/health";
        var timeout = data.GetInt("timeoutSeconds");
        if (timeout == 0) timeout = 30;

        _logger.LogInformation("🔍 Checking health of API: {ApiUrl}", apiUrl);

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(timeout);

            var startTime = DateTime.UtcNow;
            var response = await client.GetAsync(apiUrl, context.CancellationToken);
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "✅ API is healthy - Status: {StatusCode}, Response time: {Duration}ms",
                    (int)response.StatusCode, duration);
            }
            else
            {
                _logger.LogWarning(
                    "⚠️ API returned error - Status: {StatusCode}",
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ API health check failed for {ApiUrl}", apiUrl);
            throw; // Re-throw to trigger retry if configured
        }
    }
}
