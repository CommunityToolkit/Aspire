using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Quartz;

namespace CommunityToolkit.Aspire.Hosting.Quartz;

/// <summary>
/// Extension methods for adding Quartz.NET resources to an Aspire application.
/// </summary>
public static class QuartzResourceExtensions
{
    /// <summary>
    /// Adds a Quartz.NET background job scheduler resource to the application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="schedulerName">The Quartz scheduler name (default: "AspireQuartzScheduler").</param>
    /// <param name="maxConcurrency">Maximum number of concurrent jobs (default: 10).</param>
    /// <returns>A resource builder for the Quartz resource.</returns>
    public static IResourceBuilder<QuartzResource> AddQuartz(
        this IDistributedApplicationBuilder builder,
        string name,
        string? schedulerName = null,
        int maxConcurrency = 10)
    {
        var resource = new QuartzResource(name)
        {
            SchedulerName = schedulerName ?? "AspireQuartzScheduler",
            MaxConcurrency = maxConcurrency
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Configures the Quartz resource to use a SQL Server database for job storage.
    /// </summary>
    /// <param name="builder">The Quartz resource builder.</param>
    /// <param name="database">The SQL Server database resource.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<QuartzResource> WithDatabase(
        this IResourceBuilder<QuartzResource> builder,
        IResourceBuilder<IResourceWithConnectionString> database)
    {
        var connectionString = database.Resource.ConnectionStringExpression.ValueExpression;

        // Detect provider from connection string or resource type
        if (database.Resource.GetType().Name.Contains("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            builder.Resource.Provider = DatabaseProvider.PostgreSQL;
        }
        else
        {
            builder.Resource.Provider = DatabaseProvider.SqlServer;
        }

        builder.WithReference(database);
        return builder;
    }

    /// <summary>
    /// Disables automatic database migration on startup.
    /// </summary>
    /// <param name="builder">The Quartz resource builder.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<QuartzResource> WithoutMigration(
        this IResourceBuilder<QuartzResource> builder)
    {
        builder.Resource.EnableMigration = false;
        return builder;
    }

    /// <summary>
    /// Configures the idempotency key expiration time.
    /// </summary>
    /// <param name="builder">The Quartz resource builder.</param>
    /// <param name="expiration">The expiration time for idempotency keys.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<QuartzResource> WithIdempotencyExpiration(
        this IResourceBuilder<QuartzResource> builder,
        TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Idempotency expiration must be positive.", nameof(expiration));
        }

        builder.Resource.IdempotencyExpiration = expiration;
        return builder;
    }

    /// <summary>
    /// Configures the maximum number of concurrent jobs.
    /// </summary>
    /// <param name="builder">The Quartz resource builder.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent jobs.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<QuartzResource> WithMaxConcurrency(
        this IResourceBuilder<QuartzResource> builder,
        int maxConcurrency)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentException("Max concurrency must be at least 1.", nameof(maxConcurrency));
        }

        builder.Resource.MaxConcurrency = maxConcurrency;
        return builder;
    }
}
