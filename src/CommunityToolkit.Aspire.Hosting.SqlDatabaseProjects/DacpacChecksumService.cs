using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Security.Cryptography;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal class DacpacChecksumService : IDacpacChecksumService
{
    public async Task<string?> CheckIfDeployedAsync(string dacpacPath, string targetConnectionString, ILogger deploymentSkipLogger, CancellationToken cancellationToken)
    {
        var targetDatabaseName = GetDatabaseName(targetConnectionString);

        var dacpacPathChecksum = GetStringChecksum(dacpacPath);

        var dacpacChecksum = await GetChecksumAsync(dacpacPath);
        
        using (var connection = new SqlConnection(targetConnectionString))
        {
            try
            {
                // Try to connect to the target database to see it exists and fail fast if it does not.
                await connection.OpenAsync(SqlConnectionOverrides.OpenWithoutRetry, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is SqlException)
            {
                deploymentSkipLogger.LogWarning(ex, "Target database {TargetDatabase} is not available.", targetDatabaseName);
                return dacpacChecksum;
            }
 
            var deployed = await CheckExtendedPropertyAsync(connection, dacpacPathChecksum, dacpacChecksum, cancellationToken);

            if (deployed)
            {
                deploymentSkipLogger.LogInformation("The .dacpac with checksum {DacpacChecksum} has already been deployed to database {TargetDatabaseName}.", dacpacChecksum, targetDatabaseName);
                return null;
            }

            deploymentSkipLogger.LogInformation("The .dacpac with checksum {DacpacChecksum} has not been deployed to database {TargetDatabaseName}.", dacpacChecksum, targetDatabaseName);

            return dacpacChecksum;
        }
    }

    public async Task SetChecksumAsync(string dacpacPath, string targetConnectionString, string dacpacChecksum, ILogger deploymentSkipLogger, CancellationToken cancellationToken)
    {
        var targetDatabaseName = GetDatabaseName(targetConnectionString);

        var dacpacPathChecksum = GetStringChecksum(dacpacPath);
        
        using (var connection = new SqlConnection(targetConnectionString))
        {
            await connection.OpenAsync(SqlConnectionOverrides.OpenWithoutRetry, cancellationToken);
                    
            await UpdateExtendedPropertyAsync(connection, dacpacPathChecksum, dacpacChecksum, cancellationToken);

            deploymentSkipLogger.LogInformation("The .dacpac with checksum {DacpacChecksum} has been registered in database {TargetDatabaseName}.", dacpacChecksum, targetDatabaseName);
        }
    }

    private static string GetDatabaseName(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return builder.InitialCatalog;
    }

    private static async Task<string> GetChecksumAsync(string file)
    {
        var output = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());

        System.IO.Compression.ZipFile.ExtractToDirectory(file, output);

        var bytes = await File.ReadAllBytesAsync(Path.Join(output, "model.xml"));

        var predeployPath = Path.Join(output, "predeploy.sql");
        
        if (File.Exists(predeployPath))
        {
            var predeployBytes = await File.ReadAllBytesAsync(predeployPath);
            bytes = bytes.Concat(predeployBytes).ToArray();
        }

        var postdeployPath = Path.Join(output, "postdeploy.sql");

        if (File.Exists(postdeployPath))
        {
            var postdeployBytes = await File.ReadAllBytesAsync(postdeployPath);
            bytes = bytes.Concat(postdeployBytes).ToArray();
        }

        using var sha = SHA256.Create();
        var checksum = sha.ComputeHash(bytes);

        // Clean up the extracted files
        try
        {
            Directory.Delete(output, true);
        }
        catch
        {
            // Ignore any errors during cleanup
        }

        return BitConverter.ToString(checksum).Replace("-", string.Empty);
    }

    private static string GetStringChecksum(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        using var sha = SHA256.Create();
        var checksum = sha.ComputeHash(bytes);
        return BitConverter.ToString(checksum).Replace("-", string.Empty);
    }

    private static async Task<bool> CheckExtendedPropertyAsync(SqlConnection connection, string dacpacPathChecksum, string dacpacChecksum, CancellationToken cancellationToken)
    {
        var command = new SqlCommand(
            @$"SELECT CAST(1 AS BIT) FROM fn_listextendedproperty(NULL, DEFAULT, DEFAULT, DEFAULT, DEFAULT, DEFAULT, DEFAULT)
            WHERE [value] = @Expected
            AND [name] = @dacpacId;",
            connection);

        command.Parameters.AddRange(GetParameters(dacpacChecksum, dacpacPathChecksum));

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result == null ? false : (bool)result;
    }

    private static async Task UpdateExtendedPropertyAsync(SqlConnection connection, string dacpacPathChecksum, string dacpacChecksum, CancellationToken cancellationToken)
    {
        var command = new SqlCommand($@"
            IF EXISTS
            (
                SELECT 1 FROM fn_listextendedproperty(null, default, default, default, default, default, default)
                WHERE [name] = @dacpacId
            )
            BEGIN
                EXEC sp_updateextendedproperty @name = @dacpacId, @value = @Expected;
            END 
            ELSE 
            BEGIN
                EXEC sp_addextendedproperty @name = @dacpacId, @value = @Expected;
            END;",
            connection);

        command.Parameters.AddRange(GetParameters(dacpacChecksum, dacpacPathChecksum));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqlParameter[] GetParameters(string dacpacChecksum, string dacpacPathChecksum)
    {
        return
        [
            new SqlParameter("@Expected", SqlDbType.VarChar)
            {
                Value = dacpacChecksum
            },
            new SqlParameter("@dacpacId", SqlDbType.NVarChar, 128)
            {
                Value = dacpacPathChecksum
            },
        ];
    }
}
