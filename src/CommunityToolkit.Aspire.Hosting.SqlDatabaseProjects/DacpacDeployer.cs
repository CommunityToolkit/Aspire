using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Provides the actual implementation of the <see cref="IDacpacDeployer"/> interface.
/// </summary>
internal class DacpacDeployer : IDacpacDeployer
{
    /// <inheritdoc cref="IDacpacDeployer.Deploy(string, string, string, ILogger, CancellationToken)" />
    public void Deploy(string dacpacPath, string targetConnectionString, string targetDatabaseName, ILogger deploymentLogger, CancellationToken cancellationToken)
    {
        var dacPackage = DacPackage.Load(dacpacPath, DacSchemaModelStorageType.Memory);
        var dacServices = new DacServices(targetConnectionString);
        dacServices.Message += (sender, args) => deploymentLogger.LogInformation(args.Message.ToString());
        dacServices.Deploy(dacPackage, targetDatabaseName, true, new DacDeployOptions(), cancellationToken);
    }
}
