using System.Diagnostics;
using Aspire.Quartz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aspire.Quartz;

/// <summary>
/// Extension methods for adding Quartz background job client to the service collection.
/// </summary>
public static class QuartzClientExtensions
{
    /// <summary>
    /// Adds Quartz background job client to the service collection.
    /// This adds production-ready features: idempotency, OpenTelemetry, and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionName">The name of the connection string (default: "quartzdb").</param>
    /// <param name="idempotencyExpiration">The expiration time for idempotency keys (default: 7 days).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQuartzClient(
        this IServiceCollection services,
        string connectionName = "quartzdb",
        TimeSpan? idempotencyExpiration = null)
    {
        var expiration = idempotencyExpiration ?? QuartzConstants.DefaultIdempotencyExpiration;

        // Register ActivitySource for OpenTelemetry
        services.AddSingleton(new ActivitySource("Aspire.Quartz.Client"));

        // Register connection factory for idempotency store
        services.AddSingleton<IDbConnectionFactory>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<DbConnectionFactory>>();

            var connectionString = configuration.GetConnectionString(connectionName);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{connectionName}' not found. " +
                    "Ensure the database resource is referenced in the AppHost using WithReference().");
            }

            return new DbConnectionFactory(connectionString, logger);
        });

        // Register idempotency store
        services.AddSingleton<IIdempotencyStore>(sp =>
        {
            var connectionFactory = sp.GetRequiredService<IDbConnectionFactory>();
            return new IdempotencyStore(connectionFactory, expiration);
        });

        // Register BackgroundJobClient using ISchedulerFactory
        // Note: ISchedulerFactory is registered by calling AddQuartz() first
        services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();

        // Add health check
        services.AddHealthChecks()
            .AddCheck<QuartzHealthCheck>("quartz", tags: new[] { "ready", "quartz" });

        return services;
    }
}
