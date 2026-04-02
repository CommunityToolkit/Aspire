using System.Data;

namespace Aspire.Quartz;

/// <summary>
/// Factory for creating database connections.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <returns>A new database connection instance.</returns>
    IDbConnection CreateConnection();

    /// <summary>
    /// Gets the database provider type.
    /// </summary>
    DatabaseProvider Provider { get; }
}

/// <summary>
/// Database provider types supported by the Quartz integration.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// Microsoft SQL Server
    /// </summary>
    SqlServer,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSQL
}
