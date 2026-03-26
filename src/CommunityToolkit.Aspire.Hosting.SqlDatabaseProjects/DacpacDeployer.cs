using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;
using System.Data.Common;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Provides the actual implementation of the <see cref="IDacpacDeployer"/> interface.
/// </summary>
internal class DacpacDeployer : IDacpacDeployer
{
    /// <inheritdoc cref="IDacpacDeployer.Deploy(string, DacDeployOptions, string, string, ILogger, CancellationToken)" />
    public void Deploy(string dacpacPath, DacDeployOptions options, string targetConnectionString, string? targetDatabaseName, ILogger deploymentLogger, CancellationToken cancellationToken)
    {
        using var dacPackage = DacPackage.Load(dacpacPath, DacSchemaModelStorageType.Memory);

        if (string.IsNullOrWhiteSpace(targetDatabaseName))
        {
            targetDatabaseName = GetDatabaseName(targetConnectionString);
        }

        var dacServices = new DacServices(targetConnectionString);
        dacServices.Message += (sender, args) => deploymentLogger.LogInformation(args.Message.ToString());
        dacServices.Deploy(dacPackage, targetDatabaseName, true, options, cancellationToken);
    }

    internal static string GetDatabaseName(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        DbConnectionStringBuilder builder = new()
        {
            ConnectionString = connectionString
        };

        foreach (object keyObject in builder.Keys)
        {
            if (keyObject is not string key)
            {
                continue;
            }

            var normalizedKey = key.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (!string.Equals(normalizedKey, "Database", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedKey, "InitialCatalog", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (builder[key] is string databaseName && !string.IsNullOrWhiteSpace(databaseName))
            {
                return databaseName;
            }
        }

        throw new InvalidOperationException("The target connection string must include a Database or Initial Catalog value when no target database name is provided.");
    }
}
