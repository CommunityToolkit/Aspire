using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Abstracts the check of the .dacpac file already having been deployed to the target SQL Server database.
/// </summary>
internal interface IDacpacChecksumService
{
    /// <summary>
    /// Checks if the <paramref name="dacpacPath" /> file has already been deployed to the specified <paramref name="targetConnectionString" />
    /// </summary>
    /// <param name="dacpacPath">Path to the .dacpac file to deploy.</param>
    /// <param name="targetConnectionString">Connection string to the SQL Server.</param>
    /// <param name="deploymentSkipLogger">An <see cref="ILogger" /> to write the log to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the deployment operation.</param>
    /// <returns>the checksum calculated for the .dacpac if it has not been deployed, otherwise null</returns>
    Task<string?> CheckIfDeployedAsync(string dacpacPath, string targetConnectionString, ILogger deploymentSkipLogger, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the checksum extended property on the target database to indicate that the <paramref name="dacpacPath" /> file has been deployed.
    /// </summary>
    /// <param name="dacpacPath">Path to the .dacpac file to deploy.</param>
    /// <param name="targetConnectionString">Connection string to the SQL Server.</param>
    /// <param name="dacpacChecksum">Checksum for the .dacpac </param>
    /// <param name="deploymentSkipLogger">An <see cref="ILogger" /> to write the log to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the deployment operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetChecksumAsync(string dacpacPath, string targetConnectionString, string dacpacChecksum, ILogger deploymentSkipLogger, CancellationToken cancellationToken);
}
