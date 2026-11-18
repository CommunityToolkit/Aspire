using Microsoft.Extensions.AI;

namespace CommunityToolkit.Aspire.Hosting.VLLM.ConsumerApp;

/// <summary>
/// Sends a single prompt to the vLLM-backed <see cref="IChatClient"/>, logs the response, and stops the application.
/// </summary>
/// <param name="chatClient">The chat client connected to the vLLM server.</param>
/// <param name="logger">The logger for the response or request failure.</param>
/// <param name="lifetime">The application lifetime used to stop after the request.</param>
public sealed class Worker(IChatClient chatClient, ILogger<Worker> logger, IHostApplicationLifetime lifetime) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync("In one sentence, what is Aspire?", cancellationToken: stoppingToken);
            logger.LogInformation("vLLM response: {Response}", response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to call the vLLM chat client.");
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
