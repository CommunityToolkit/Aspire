using Aspire.Hosting.ApplicationModel;
using HealthChecks.SqlServer;


namespace Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

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
