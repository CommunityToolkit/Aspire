using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Webhook notification job - demonstrates HTTP POST with retry
/// </summary>
public class WebhookNotificationJob : IJob
{
    private readonly ILogger<WebhookNotificationJob> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public const string Name = nameof(WebhookNotificationJob);
    public const string Group = "notifications";

    public WebhookNotificationJob(
        ILogger<WebhookNotificationJob> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var webhookUrl = data.GetString("webhookUrl") ?? "https://webhook.site/test";
        var payload = data.GetString("payload") ?? "{}";
        var retryCount = data.GetInt("retryCount");

        _logger.LogInformation(
            "🔔 Sending webhook notification to {Url} (Retry: {Retry})",
            webhookUrl, retryCount);

        try
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(webhookUrl, content, context.CancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Webhook delivered successfully");
            }
            else
            {
                _logger.LogWarning("⚠️ Webhook failed with status: {Status}", response.StatusCode);

                // Increment retry count for next attempt
                data.Put("retryCount", retryCount + 1);
                throw new Exception($"Webhook failed with status {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Webhook delivery failed");
            throw;
        }
    }
}
