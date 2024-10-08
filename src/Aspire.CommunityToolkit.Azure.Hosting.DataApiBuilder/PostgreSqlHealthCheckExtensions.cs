using Aspire.Hosting.ApplicationModel;
using HealthChecks.NpgSql;

namespace Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

public static class PostgreSqlHealthCheckExtensions
{
    /// <summary>
    /// Adds a health check to the PostgreSQL server resource.
    /// </summary>
    public static IResourceBuilder<PostgresServerResource> WithHealthCheck(this IResourceBuilder<PostgresServerResource> builder)
    {
        return builder.WithAnnotation(HealthCheckAnnotation.Create(cs => new NpgSqlHealthCheck(new NpgSqlHealthCheckOptions(cs))));
    }

    /// <summary>
    /// Adds a health check to the PostgreSQL database resource.
    /// </summary>
    public static IResourceBuilder<PostgresDatabaseResource> WithHealthCheck(this IResourceBuilder<PostgresDatabaseResource> builder)
    {
        return builder.WithAnnotation(HealthCheckAnnotation.Create(cs => new NpgSqlHealthCheck(new NpgSqlHealthCheckOptions(cs))));
    }
}
