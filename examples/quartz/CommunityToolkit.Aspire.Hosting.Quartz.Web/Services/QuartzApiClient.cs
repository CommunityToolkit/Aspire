using System.Net.Http.Json;

namespace QuartzSample.Web.Services;

public class QuartzApiClient
{
    private readonly HttpClient _httpClient;

    public QuartzApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Get all jobs
    public async Task<List<JobInfo>> GetAllJobsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<JobInfo>>("/jobs") ?? new();
    }

    // Schedule one-time job
    public async Task<ScheduleResponse> ScheduleOnceAsync(ScheduleOnceRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/jobs/schedule-once", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleResponse>() ?? new();
    }

    // Schedule cron job
    public async Task<ScheduleResponse> ScheduleCronAsync(ScheduleCronRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/jobs/schedule-cron", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleResponse>() ?? new();
    }

    // Schedule repeating job
    public async Task<ScheduleResponse> ScheduleRepeatAsync(ScheduleRepeatRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/jobs/schedule-repeat", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleResponse>() ?? new();
    }

    // Cancel job
    public async Task<bool> CancelJobAsync(string jobId, string jobGroup)
    {
        var response = await _httpClient.DeleteAsync($"/jobs/{jobId}/{jobGroup}");
        return response.IsSuccessStatusCode;
    }
}

// Models
public record JobInfo
{
    public string JobId { get; init; } = string.Empty;
    public string JobGroup { get; init; } = string.Empty;
    public string JobType { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? NextFireTime { get; init; }
    public DateTime? PreviousFireTime { get; init; }
}

public record ScheduleOnceRequest(string Endpoint, DateTimeOffset StartTime);
public record ScheduleCronRequest(string CronExpression, int DaysToKeep, string TableName);
public record ScheduleRepeatRequest(string ReportType, string Recipients, int IntervalMinutes, int? RepeatCount);

public record ScheduleResponse
{
    public string JobId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string JobType { get; init; } = string.Empty;
    public string? CronExpression { get; init; }
    public int? IntervalMinutes { get; init; }
    public int? RepeatCount { get; init; }
}
