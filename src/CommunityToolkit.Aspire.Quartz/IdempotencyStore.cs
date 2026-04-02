using System.Data;
using System.Data.Common;

namespace Aspire.Quartz;

internal sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly TimeSpan _expiration;

    public IdempotencyStore(IDbConnectionFactory connectionFactory, TimeSpan expiration)
    {
        _connectionFactory = connectionFactory;
        _expiration = expiration;
    }

    public async Task<bool> TryAcquireAsync(string key, string jobId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenConnectionAsync(connection, cancellationToken);

        // Use INSERT directly and catch unique constraint violation
        // This is atomic and prevents race conditions
        const string insertSql = @"
            INSERT INTO QRTZ_IDEMPOTENCY_KEYS (IDEMPOTENCY_KEY, JOB_ID, CREATED_AT, EXPIRES_AT)
            VALUES (@Key, @JobId, @CreatedAt, @ExpiresAt)";

        using var command = connection.CreateCommand();
        command.CommandText = insertSql;
        AddParameter(command, "@Key", key);
        AddParameter(command, "@JobId", jobId);
        AddParameter(command, "@CreatedAt", DateTime.UtcNow);
        AddParameter(command, "@ExpiresAt", DateTime.UtcNow.Add(_expiration));

        try
        {
            await ExecuteNonQueryAsync(command, cancellationToken);
            return true; // Successfully acquired
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // Key already exists - this is expected for duplicates
            return false;
        }
    }

    /// <summary>
    /// Checks if exception is a unique constraint violation.
    /// </summary>
    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        // SQL Server: Error 2627 (unique constraint) or 2601 (unique index)
        if (ex.Message.Contains("2627") || ex.Message.Contains("2601"))
            return true;

        // PostgreSQL: Error 23505 (unique violation)
        if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
            return true;

        return false;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenConnectionAsync(connection, cancellationToken);

        const string sql = @"
            SELECT COUNT(*)
            FROM QRTZ_IDEMPOTENCY_KEYS
            WHERE IDEMPOTENCY_KEY = @Key AND EXPIRES_AT > @Now";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@Key", key);
        AddParameter(command, "@Now", DateTime.UtcNow);

        var result = await ExecuteScalarAsync(command, cancellationToken);
        var count = Convert.ToInt32(result);
        return count > 0;
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

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

internal interface IIdempotencyStore
{
    Task<bool> TryAcquireAsync(string key, string jobId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
