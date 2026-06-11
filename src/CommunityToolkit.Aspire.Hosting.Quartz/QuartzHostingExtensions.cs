using Aspire.Quartz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CommunityToolkit.Aspire.Hosting.Quartz;

/// <summary>
/// Extension methods for adding Quartz.NET hosting services to an application.
/// </summary>
public static class QuartzHostingExtensions
{
    /// <summary>
    /// Adds Quartz.NET background job scheduler to the service collection.
    /// This should be called in worker services that process background jobs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionName">The name of the connection string (default: "quartzdb").</param>
    /// <param name="enableMigration">Whether to run database migration on startup (default: true).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQuartzWorker(
        this IServiceCollection services,
        string connectionName = "quartzdb",
        bool enableMigration = true)
    {
        // Register connection factory for migration service (simplified)
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

        // Register migration service if enabled
        if (enableMigration)
        {
            services.AddHostedService(sp =>
            {
                var connectionFactory = sp.GetRequiredService<IDbConnectionFactory>();
                var logger = sp.GetRequiredService<ILogger<QuartzMigrationService>>();
                var provider = connectionFactory.Provider;

                return new QuartzMigrationService(connectionFactory, logger, provider, enableMigration);
            });
        }

        // Configure Quartz.NET
        services.AddQuartz(q =>
        {
            q.SchedulerId = "AspireQuartzScheduler";
            q.MaxBatchSize = 10;
        });

        // Add Quartz hosted service
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}
