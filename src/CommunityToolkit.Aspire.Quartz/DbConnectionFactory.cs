using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Aspire.Quartz;

/// <summary>
/// Factory for creating database connections with simple provider detection.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;
    private readonly ILogger<DbConnectionFactory> _logger;

    /// <summary>
    /// Gets the detected database provider type.
    /// </summary>
    public DatabaseProvider Provider => _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="logger">The logger instance.</param>
    public DbConnectionFactory(
        string connectionString,
        ILogger<DbConnectionFactory> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionString = connectionString;
        _provider = DetectProvider(connectionString);
        _logger = logger;

        _logger.LogInformation("Using {Provider} database provider", _provider);
    }

    /// <summary>
    /// Creates a new database connection based on the detected provider.
    /// </summary>
    /// <returns>A new database connection instance.</returns>
    public IDbConnection CreateConnection()
    {
        _logger.LogDebug("Creating new {Provider} connection", _provider);

        return _provider switch
        {
            DatabaseProvider.PostgreSQL => new NpgsqlConnection(_connectionString),
            DatabaseProvider.SqlServer => new SqlConnection(_connectionString),
            _ => throw new NotSupportedException($"Provider {_provider} not supported")
        };
    }

    /// <summary>
    /// Simple provider detection - KISS principle.
    /// </summary>
    private static DatabaseProvider DetectProvider(string connectionString) =>
        connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            ? DatabaseProvider.PostgreSQL
            : DatabaseProvider.SqlServer;
}
