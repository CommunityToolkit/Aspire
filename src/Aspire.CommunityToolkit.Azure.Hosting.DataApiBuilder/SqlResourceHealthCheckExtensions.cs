using Aspire.Hosting.ApplicationModel;
using HealthChecks.SqlServer;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <remarks>
/// This has been copied from the David Fowler davidfowl/WaitForDependenciesAspire project and will likely be removed in the future.
/// </remarks>
[Experimental("CTASPIRE002", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
public static class SqlResourceHealthCheckExtensions
{
    /// <summary>
    /// Adds a health check to the SQL Server server resource.
    /// </summary>
    public static IResourceBuilder<SqlServerServerResource> WithHealthCheck(this IResourceBuilder<SqlServerServerResource> builder)
    {
        return builder.WithSqlHealthCheck(cs => new SqlServerHealthCheckOptions { ConnectionString = cs });
    }

    /// <summary>
    /// Adds a health check to the SQL Server database resource.
    /// </summary>
    public static IResourceBuilder<SqlServerDatabaseResource> WithHealthCheck(this IResourceBuilder<SqlServerDatabaseResource> builder)
    {
        return builder.WithSqlHealthCheck(cs => new SqlServerHealthCheckOptions { ConnectionString = cs });
    }

    /// <summary>
    /// Adds a health check to the SQL Server server resource with a specific query.
    /// </summary>
    public static IResourceBuilder<SqlServerServerResource> WithHealthCheck(this IResourceBuilder<SqlServerServerResource> builder, string query)
    {
        return builder.WithSqlHealthCheck(cs => new SqlServerHealthCheckOptions { ConnectionString = cs, CommandText = query });
    }

    /// <summary>
    /// Adds a health check to the SQL Server database resource  with a specific query.
    /// </summary>
    public static IResourceBuilder<SqlServerDatabaseResource> WithHealthCheck(this IResourceBuilder<SqlServerDatabaseResource> builder, string query)
    {
        return builder.WithSqlHealthCheck(cs => new SqlServerHealthCheckOptions { ConnectionString = cs, CommandText = query });
    }

    private static IResourceBuilder<T> WithSqlHealthCheck<T>(this IResourceBuilder<T> builder, Func<string, SqlServerHealthCheckOptions> healthCheckOptionsFactory)
        where T: IResource
    {
        return builder.WithAnnotation(HealthCheckAnnotation.Create(cs => new SqlServerHealthCheck(healthCheckOptionsFactory(cs))));
    }
}
