using HealthChecks.RavenDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.RavenDB;

internal static class HealthChecksExtensions
{
    private const string NAME = "ravendb";

    /// <summary>
    /// Add a health check for RavenDB server services.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionStringFactory">A factory to build the connection string to use.</param>
    /// <param name="name">The health check name. Optional. If <c>null</c> the type name 'ravendb' will be used for the name.</param>
    /// <param name="failureStatus">that should be reported when the health check fails. Optional. If <c>null</c> then
    /// the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.</param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks. Optional.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The specified <paramref name="builder"/>.</returns>
    public static IHealthChecksBuilder AddRavenDB(
        this IHealthChecksBuilder builder,
        Func<IServiceProvider, string> connectionStringFactory,
        string? name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? NAME,
            sp => new RavenDBHealthCheck(new RavenDBOptions() { Urls = new[] { connectionStringFactory(sp) } }),
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// Add a health check for RavenDB database services.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionStringFactory">A factory to build the connection string to use.</param>
    /// <param name="databaseName">The database name to check.</param>
    /// <param name="name">The health check name. Optional. If <c>null</c> the type name 'ravendb' will be used for the name.</param>
    /// <param name="failureStatus">that should be reported when the health check fails. Optional. If <c>null</c> then
    /// the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.</param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks. Optional.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The specified <paramref name="builder"/>.</returns>
    public static IHealthChecksBuilder AddRavenDB(
        this IHealthChecksBuilder builder,
        Func<IServiceProvider, string> connectionStringFactory,
        string databaseName,
        string? name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? NAME,
            sp => new RavenDBHealthCheck(new RavenDBOptions() { Urls = new[] { connectionStringFactory(sp) }, Database = databaseName }),
            failureStatus,
            tags,
            timeout));
    }
}
