using CommunityToolkit.Aspire.Quartz;

namespace QuartzSample.ApiService;

public class SendEmailJob : IJob
{
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(ILogger<SendEmailJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        var email = context.Parameters?["email"]?.ToString() ?? "unknown@example.com";
        var subject = context.Parameters?["subject"]?.ToString() ?? "No Subject";

        _logger.LogInformation(
            "Sending email to {Email} with subject '{Subject}' (JobId: {JobId}, Attempt: {Attempt})",
            email, subject, context.JobId, context.RetryAttempt);

        // Simulate email sending
        await Task.Delay(1000, cancellationToken);

        _logger.LogInformation("Email sent successfully to {Email}", email);
    }
}
