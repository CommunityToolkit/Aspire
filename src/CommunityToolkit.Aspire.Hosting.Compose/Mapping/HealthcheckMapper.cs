using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Compose.Mapping;

/// <summary>
/// Maps compose healthcheck definitions to Aspire health checks.
/// </summary>
internal static class HealthcheckMapper
{
    public static void Map(IResourceBuilder<ContainerResource> resourceBuilder, ComposeService service, string serviceName, IDistributedApplicationBuilder builder)
    {
        if (service.Healthcheck is not { } healthcheck)
            return;

        if (healthcheck.Disable == true)
            return;

        string[] testCommand = healthcheck.Test switch
        {
            string str => ServiceToResourceMapper.ParseStringOrList(str),
            List<object> list => [.. list.Select(item => item.ToString()!)],
            _ => []
        };

        if (testCommand.Length == 0)
            return;

        string healthCheckName = $"compose-{serviceName}";

        bool isCmdShell = testCommand[0] is "CMD-SHELL";
        string command = isCmdShell
            ? string.Join(" ", testCommand[1..])
            : string.Join(" ", testCommand.Where(t => t is not "CMD" and not "NONE"));

        if (string.IsNullOrEmpty(command))
            return;

        TimeSpan interval = ParseDuration(healthcheck.Interval) ?? TimeSpan.FromSeconds(30);
        TimeSpan timeout = ParseDuration(healthcheck.Timeout) ?? TimeSpan.FromSeconds(30);

        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(healthCheckName, new ComposeHealthCheck(serviceName, command), failureStatus: HealthStatus.Unhealthy, tags: [$"compose:{serviceName}"])
        {
            Period = interval,
            Timeout = timeout
        });

        resourceBuilder.WithHealthCheck(healthCheckName);
    }

    internal static TimeSpan? ParseDuration(string? duration)
    {
        if (string.IsNullOrEmpty(duration))
            return null;

        if (duration.EndsWith("ms") && double.TryParse(duration[..^2], out double ms))
            return TimeSpan.FromMilliseconds(ms);

        if (duration.EndsWith('s') && double.TryParse(duration[..^1], out double seconds))
            return TimeSpan.FromSeconds(seconds);

        if (duration.EndsWith('m') && double.TryParse(duration[..^1], out double minutes))
            return TimeSpan.FromMinutes(minutes);

        return null;
    }
}
