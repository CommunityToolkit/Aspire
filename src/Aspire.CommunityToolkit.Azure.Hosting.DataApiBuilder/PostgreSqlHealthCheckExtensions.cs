using Aspire.Hosting.ApplicationModel;
using HealthChecks.NpgSql;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <remarks>
/// This has been copied from the David Fowler davidfowl/WaitForDependenciesAspire project and will likely be removed in the future.
/// </remarks>
[Experimental("CTASPIRE009", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
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
