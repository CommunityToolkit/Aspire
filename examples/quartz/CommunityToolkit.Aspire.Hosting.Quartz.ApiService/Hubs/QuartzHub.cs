using Microsoft.AspNetCore.SignalR;

namespace QuartzSample.ApiService.Hubs;

/// <summary>
/// SignalR Hub for real-time Quartz job updates
/// </summary>
public class QuartzHub : Hub
{
    private readonly ILogger<QuartzHub> _logger;

    public QuartzHub(ILogger<QuartzHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Broadcast job scheduled event to all clients
    /// </summary>
    public async Task NotifyJobScheduled(string jobId, string jobType, string message)
    {
        await Clients.All.SendAsync("JobScheduled", new
        {
            jobId,
            jobType,
            message,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast job started event to all clients
    /// </summary>
    public async Task NotifyJobStarted(string jobId, string jobType)
    {
        await Clients.All.SendAsync("JobStarted", new
        {
            jobId,
            jobType,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast job completed event to all clients
    /// </summary>
    public async Task NotifyJobCompleted(string jobId, string jobType, bool success, string? error = null)
    {
        await Clients.All.SendAsync("JobCompleted", new
        {
            jobId,
            jobType,
            success,
            error,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast job cancelled event to all clients
    /// </summary>
    public async Task NotifyJobCancelled(string jobId, string jobGroup)
    {
        await Clients.All.SendAsync("JobCancelled", new
        {
            jobId,
            jobGroup,
            timestamp = DateTime.UtcNow
        });
    }
}
