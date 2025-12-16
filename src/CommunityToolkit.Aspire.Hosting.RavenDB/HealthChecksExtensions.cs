using HealthChecks.RavenDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Data.Common;
using System.Security.Cryptography.X509Certificates;

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
    /// <param name="certificate">The client certificate to use for the connection. Optional.</param>
    /// <param name="failureStatus">that should be reported when the health check fails. Optional. If <c>null</c> then
    /// the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.</param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks. Optional.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The specified <paramref name="builder"/>.</returns>
    public static IHealthChecksBuilder AddRavenDB(
        this IHealthChecksBuilder builder,
        Func<IServiceProvider, string> connectionStringFactory,
        string? name = default,
        X509Certificate2? certificate = null,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? NAME,
            sp =>
            {
                var connectionString = ValidateConnectionString(connectionStringFactory, sp);
                return new RavenDBHealthCheck(new RavenDBOptions { Urls = new[] { connectionString }, Certificate = certificate});
            },
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
    /// <param name="certificate">The client certificate to use for the connection. Optional.</param>
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
        X509Certificate2? certificate = null,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? NAME,
            sp =>
            {
                var connectionString = ValidateConnectionString(connectionStringFactory, sp);
                return new RavenDBHealthCheck(new RavenDBOptions
                {
                    Urls = new[] { connectionString },
                    Database = databaseName,
                    Certificate = certificate
                });
            },
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// Validates that the connection string is not null or empty.
    /// </summary>
    /// <param name="connectionStringFactory">The factory to generate the connection string.</param>
    /// <param name="serviceProvider">The service provider instance.</param>
    /// <returns>A valid, non-empty connection string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the connection string is null or empty.</exception>
    private static string ValidateConnectionString(Func<IServiceProvider, string> connectionStringFactory, IServiceProvider serviceProvider)
    {
        var connectionString = connectionStringFactory(serviceProvider);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Failed to generate a valid RavenDB connection string. The result cannot be null or empty.");
        }

        var connectionBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        if (connectionBuilder.TryGetValue("URL", out var url) && url is string serverUrl)
        {
            connectionString = serverUrl;
        }
        else
        {
            throw new InvalidOperationException("Connection string is unavailable");
        }

        return connectionString;
    }
}
