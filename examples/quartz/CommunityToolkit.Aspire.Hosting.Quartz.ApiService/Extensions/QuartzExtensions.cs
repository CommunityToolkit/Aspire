using Quartz;

namespace QuartzSample.ApiService.Extensions;

public static class QuartzExtensions
{
    /// <summary>
    /// Add a job and trigger from configuration
    /// </summary>
    public static void AddJobAndTrigger<T>(
        this IServiceCollectionQuartzConfigurator quartz,
        IConfiguration config)
        where T : IJob
    {
        string jobName = typeof(T).Name;
        var configKey = $"Quartz:{jobName}";
        var cronSchedule = config[configKey];

        if (string.IsNullOrEmpty(cronSchedule))
        {
            throw new Exception($"No Quartz.NET Cron schedule found for job in configuration at {configKey}");
        }

        var jobKey = new JobKey(jobName);

        quartz.AddJob<T>(opts => opts
            .WithIdentity(jobKey)
            .StoreDurably());

        quartz.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"{jobName}-trigger")
            .WithCronSchedule(cronSchedule));
    }
}
