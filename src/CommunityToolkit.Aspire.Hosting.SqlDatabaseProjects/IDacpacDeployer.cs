using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Abstracts the actual deployment of the .dacpac file to a SQL Server database.
/// </summary>
internal interface IDacpacDeployer
{
    /// <summary>
    /// Deployes the provided <paramref name="dacpacPath">.dacpac</paramref> file to the specified <paramref name="targetConnectionString">SQL Server</paramref>
    /// using the provided <paramref name="targetDatabaseName">database name</paramref>.
    /// </summary>
    /// <param name="dacpacPath">Path to the .dacpac file to deploy.</param>
    /// <param name="options">Instance of <see cref="T:DacDeployOptions" /> that specifies properties that affect various aspects of the deployment.</param>
    /// <param name="targetConnectionString">Connection string to the SQL Server.</param>
    /// <param name="targetDatabaseName">Name of the target database to deploy to.</param>
    /// <param name="deploymentLogger">An <see cref="ILogger" /> to write the deployment log to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the deployment operation.</param>
    void Deploy(string dacpacPath, DacDeployOptions options, string targetConnectionString, string targetDatabaseName, ILogger deploymentLogger, CancellationToken cancellationToken);
}
