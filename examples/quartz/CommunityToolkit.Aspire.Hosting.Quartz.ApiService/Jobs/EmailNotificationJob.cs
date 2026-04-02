using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Email notification job - demonstrates simple job with parameters
/// </summary>
[DisallowConcurrentExecution]
public class EmailNotificationJob : IJob
{
    private readonly ILogger<EmailNotificationJob> _logger;

    public const string Name = nameof(EmailNotificationJob);
    public const string Group = "notifications";

    public EmailNotificationJob(ILogger<EmailNotificationJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var recipient = data.GetString("recipient") ?? "user@example.com";
        var subject = data.GetString("subject") ?? "Notification";
        var body = data.GetString("body") ?? "This is a test notification";

        _logger.LogInformation(
            "📧 Sending email to {Recipient} - Subject: {Subject}",
            recipient, subject);

        // Simulate email sending
        await Task.Delay(1000, context.CancellationToken);

        _logger.LogInformation("✅ Email sent successfully to {Recipient}", recipient);
    }
}
