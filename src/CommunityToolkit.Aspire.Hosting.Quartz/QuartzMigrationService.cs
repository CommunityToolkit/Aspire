using System.Data;
using System.Data.Common;
using Aspire.Quartz;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Quartz;

/// <summary>
/// Background service that runs database migrations for Quartz.NET tables on startup.
/// </summary>
internal sealed class QuartzMigrationService : IHostedService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<QuartzMigrationService> _logger;
    private readonly DatabaseProvider _provider;
    private readonly bool _enableMigration;

    public QuartzMigrationService(
        IDbConnectionFactory connectionFactory,
        ILogger<QuartzMigrationService> logger,
        DatabaseProvider provider,
        bool enableMigration)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _provider = provider;
        _enableMigration = enableMigration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_enableMigration)
        {
            _logger.LogInformation("Database migration is disabled");
            return;
        }

        try
        {
            _logger.LogInformation("Checking if Quartz tables exist...");

            if (await TablesExistAsync(cancellationToken))
            {
                _logger.LogInformation("Quartz tables already exist, skipping migration");
                return;
            }

            _logger.LogInformation("Running Quartz database migration for {Provider}...", _provider);
            await ExecuteScriptAsync(cancellationToken);
            _logger.LogInformation("Quartz database migration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run Quartz database migration");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<bool> TablesExistAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenConnectionAsync(connection, cancellationToken);

        const string sql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = 'QRTZ_JOB_DETAILS'";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var result = await ExecuteScalarAsync(command, cancellationToken);
        var count = Convert.ToInt32(result);
        return count > 0;
    }

    private async Task ExecuteScriptAsync(CancellationToken cancellationToken)
    {
        var script = _provider == DatabaseProvider.SqlServer
            ? SqlServerMigrationScript.Script
            : PostgreSqlMigrationScript.Script;

        using var connection = _connectionFactory.CreateConnection();
        await OpenConnectionAsync(connection, cancellationToken);

        // Split script by GO statements for SQL Server or semicolons for PostgreSQL
        var separator = _provider == DatabaseProvider.SqlServer ? "GO" : ";";
        var statements = script.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var statement in statements)
        {
            var trimmed = statement.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            using var command = connection.CreateCommand();
            command.CommandText = trimmed;
            await ExecuteNonQueryAsync(command, cancellationToken);
        }
    }

    private static async Task OpenConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }
    }

    private static async Task<object?> ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand)
        {
            return await dbCommand.ExecuteScalarAsync(cancellationToken);
        }
        return command.ExecuteScalar();
    }

    private static async Task<int> ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken)
    {
        if (command is DbCommand dbCommand)
        {
            return await dbCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        return command.ExecuteNonQuery();
    }
}
