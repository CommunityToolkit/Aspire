using Aspire.Quartz;
using Microsoft.AspNetCore.SignalR;
using Quartz;
using CommunityToolkit.Aspire.Hosting.Quartz.ApiService.Extensions;
using CommunityToolkit.Aspire.Hosting.Quartz.ApiService.Hubs;
using CommunityToolkit.Aspire.Hosting.Quartz.ApiService.Jobs;
using CommunityToolkit.Aspire.Hosting.Quartz.ApiService.Listeners;
using CommunityToolkit.Aspire.Hosting.Quartz.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Add SignalR
builder.Services.AddSignalR();

// Add Quartz.NET with full scheduling power (MUST be called before AddQuartzClient)
builder.Services.AddQuartz(q =>
{
    // 💡 Choose your database provider (must match DbContext above):

    // ✅ PostgreSQL (Active)
    q.UsePersistentStore(store =>
    {
        store.UsePostgres(pg =>
        {
            pg.ConnectionString = builder.Configuration.GetConnectionString("quartzdb")!;
            pg.TablePrefix = "QRTZ_";

            // Support custom schema
            var schema = builder.Configuration.GetValue<string>("Quartz:Schema");
            if (!string.IsNullOrEmpty(schema))
            {
                pg.TablePrefix = $"{schema}.QRTZ_";
            }
        });
        store.UseNewtonsoftJsonSerializer();
    });

    // 💡 SQL Server (Commented - uncomment to use)
    // q.UsePersistentStore(store =>
    // {
    //     store.UseSqlServer(builder.Configuration.GetConnectionString("quartzdb")!);
    //     store.UseNewtonsoftJsonSerializer();
    // });

    // 💡 MySQL (Commented - uncomment to use)
    // q.UsePersistentStore(store =>
    // {
    //     store.UseMySql(builder.Configuration.GetConnectionString("quartzdb")!);
    //     store.UseNewtonsoftJsonSerializer();
    // });

    // 💡 SQLite (Commented - uncomment to use)
    // q.UsePersistentStore(store =>
    // {
    //     store.UseSQLite(builder.Configuration.GetConnectionString("quartzdb")!);
    //     store.UseNewtonsoftJsonSerializer();
    // });

    // Add job listener for SignalR notifications
    q.AddJobListener<QuartzJobListener>();

    // Configure recurring jobs with job data
    q.ScheduleJob<HealthCheckJob>(trigger => trigger
        .WithIdentity("health-check-trigger")
        .StartNow()
        .WithCronSchedule("0 */5 * * * ?") // Every 5 minutes
        .UsingJobData("endpoint", "https://api.example.com/health"));

    q.ScheduleJob<DataCleanupJob>(trigger => trigger
        .WithIdentity("cleanup-trigger")
        .WithCronSchedule("0 0 3 ? * SUN") // Every Sunday at 3 AM
        .UsingJobData("daysToKeep", 30)
        .UsingJobData("tableName", "logs"));

    q.ScheduleJob<DailyReportJob>(trigger => trigger
        .WithIdentity("daily-report-trigger")
        .WithCronSchedule("0 0 2 * * ?") // Every day at 2 AM
        .UsingJobData("reportType", "daily")
        .UsingJobData("recipients", "admin@example.com"));
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

// Add Quartz client for job enqueuing (MUST be called after AddQuartz)
builder.Services.AddQuartzClient("quartzdb");

// Register our scheduler service
builder.Services.AddSingleton<QuartzJobScheduler>();

// Add services to the container
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();

// Add CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

app.UseCors();

app.MapGet("/", () => "Quartz.NET Scheduling API with Real-Time Updates 🚀");

// ===== QUARTZ.NET NATIVE SCHEDULING API (Full Power) =====

// Get all scheduled jobs
app.MapGet("/jobs", async (QuartzJobScheduler scheduler) =>
{
    var jobs = await scheduler.GetAllJobsAsync();
    return Results.Ok(jobs);
})
.WithName("GetAllJobs");

// Schedule a one-time job
app.MapPost("/jobs/schedule-once", async (
    QuartzJobScheduler scheduler,
    IHubContext<QuartzHub> hubContext,
    ScheduleOnceRequest request) =>
{
    var jobData = new JobDataMap
    {
        { "endpoint", request.Endpoint }
    };

    var jobId = await scheduler.ScheduleOneTimeJobAsync<HealthCheckJob>(
        jobData,
        request.StartTime);

    // Notify all clients
    await hubContext.Clients.All.SendAsync("JobScheduled", new
    {
        jobId,
        jobType = nameof(HealthCheckJob),
        message = $"Job scheduled to run at {request.StartTime}",
        timestamp = DateTime.UtcNow
    });

    return Results.Ok(new
    {
        jobId,
        message = $"Job scheduled to run at {request.StartTime}",
        jobType = nameof(HealthCheckJob)
    });
})
.WithName("ScheduleOnceJob");

// Schedule a recurring job with cron
app.MapPost("/jobs/schedule-cron", async (
    QuartzJobScheduler scheduler,
    IHubContext<QuartzHub> hubContext,
    ScheduleCronRequest request) =>
{
    var jobData = new JobDataMap
    {
        { "daysToKeep", request.DaysToKeep },
        { "tableName", request.TableName }
    };

    var jobId = await scheduler.ScheduleRecurringJobAsync<DataCleanupJob>(
        jobData,
        request.CronExpression);

    // Notify all clients
    await hubContext.Clients.All.SendAsync("JobScheduled", new
    {
        jobId,
        jobType = nameof(DataCleanupJob),
        message = $"Recurring job scheduled with cron: {request.CronExpression}",
        timestamp = DateTime.UtcNow
    });

    return Results.Ok(new
    {
        jobId,
        message = $"Recurring job scheduled with cron: {request.CronExpression}",
        jobType = nameof(DataCleanupJob),
        cronExpression = request.CronExpression
    });
})
.WithName("ScheduleCronJob");

// Schedule a repeating job
app.MapPost("/jobs/schedule-repeat", async (
    QuartzJobScheduler scheduler,
    IHubContext<QuartzHub> hubContext,
    ScheduleRepeatRequest request) =>
{
    var jobData = new JobDataMap
    {
        { "reportType", request.ReportType },
        { "recipients", request.Recipients }
    };

    var jobId = await scheduler.ScheduleRepeatingJobAsync<DailyReportJob>(
        jobData,
        TimeSpan.FromMinutes(request.IntervalMinutes),
        request.RepeatCount);

    // Notify all clients
    await hubContext.Clients.All.SendAsync("JobScheduled", new
    {
        jobId,
        jobType = nameof(DailyReportJob),
        message = $"Repeating job scheduled every {request.IntervalMinutes} minutes",
        timestamp = DateTime.UtcNow
    });

    return Results.Ok(new
    {
        jobId,
        message = $"Repeating job scheduled every {request.IntervalMinutes} minutes",
        jobType = nameof(DailyReportJob),
        intervalMinutes = request.IntervalMinutes,
        repeatCount = request.RepeatCount
    });
})
.WithName("ScheduleRepeatJob");

// 📧 Send Email Job (Demo)
app.MapPost("/jobs/send-email", async (
    QuartzJobScheduler scheduler,
    IHubContext<QuartzHub> hubContext,
    string recipient,
    string? subject = null,
    string? body = null) =>
{
    var jobData = new JobDataMap
    {
        { "recipient", recipient },
        { "subject", subject ?? "Test Email from Quartz" },
        { "body", body ?? "This is a test email sent via Quartz.NET job scheduler" }
    };

    var jobId = await scheduler.ScheduleOneTimeJobAsync<EmailNotificationJob>(
        jobData,
        DateTimeOffset.UtcNow.AddSeconds(5)); // Send after 5 seconds

    // Notify all clients
    await hubContext.Clients.All.SendAsync("JobScheduled", new
    {
        jobId,
        jobType = nameof(EmailNotificationJob),
        message = $"📧 Email scheduled to {recipient}",
        timestamp = DateTime.UtcNow
    });

    return Results.Ok(new
    {
        jobId,
        message = $"📧 Email job scheduled! Will send to {recipient} in 5 seconds",
        recipient,
        subject = subject ?? "Test Email from Quartz",
        checkLogs = "Watch the logs to see email being sent"
    });
})
.WithName("SendEmail")
.WithDescription("📧 Schedule an email notification job");

// Cancel a job
app.MapDelete("/jobs/{jobId}/{jobGroup}", async (
    QuartzJobScheduler scheduler,
    IHubContext<QuartzHub> hubContext,
    string jobId,
    string jobGroup) =>
{
    var result = await scheduler.CancelJobAsync(jobId, jobGroup);

    if (result)
    {
        // Notify all clients
        await hubContext.Clients.All.SendAsync("JobCancelled", new
        {
            jobId,
            jobGroup,
            timestamp = DateTime.UtcNow
        });

        return Results.Ok(new { message = "Job cancelled successfully" });
    }

    return Results.NotFound(new { message = "Job not found" });
})
.WithName("CancelJob");

// Map SignalR hub
app.MapHub<QuartzHub>("/hubs/quartz");

app.MapDefaultEndpoints();

app.Run();

// ===== REQUEST MODELS =====

record ScheduleOnceRequest(string Endpoint, DateTimeOffset StartTime);
record ScheduleCronRequest(string CronExpression, int DaysToKeep, string TableName);
record ScheduleRepeatRequest(string ReportType, string Recipients, int IntervalMinutes, int? RepeatCount);
