using Aspire.Hosting.ApplicationModel;
using Aspire.Quartz;

namespace CommunityToolkit.Aspire.Hosting.Quartz;

/// <summary>
/// Represents a Quartz.NET background job scheduler resource in an Aspire application.
/// </summary>
/// <param name="name">The name of the resource.</param>
public sealed class QuartzResource(string name) : Resource(name), IResourceWithConnectionString, IResourceWithEnvironment
{
    /// <summary>
    /// Gets or sets the scheduler name used by Quartz.NET.
    /// </summary>
    public string SchedulerName { get; set; } = "AspireQuartzScheduler";

    /// <summary>
    /// Gets or sets the maximum number of concurrent jobs.
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Gets or sets the database provider type.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    /// <summary>
    /// Gets or sets whether to automatically run database migrations on startup.
    /// </summary>
    public bool EnableMigration { get; set; } = true;

    /// <summary>
    /// Gets or sets the expiration time for idempotency keys.
    /// </summary>
    public TimeSpan IdempotencyExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets the connection string expression for the Quartz resource.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Name}");
}

