using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal class SqlProjectPublishService(IDacpacDeployer deployer, ResourceLoggerService resourceLoggerService, ResourceNotificationService resourceNotificationService, IDistributedApplicationEventing eventing, IServiceProvider serviceProvider)
{
    public async Task PublishSqlProject(SqlProjectResource sqlProject, SqlServerDatabaseResource target, CancellationToken cancellationToken)
    {
        var logger = resourceLoggerService.GetLogger(sqlProject);

        try
        {
            var dacpacPath = sqlProject.GetDacpacPath();
            if (!File.Exists(dacpacPath))
            {
                logger.LogError("SQL Server Database project package not found at path {DacpacPath}.", dacpacPath);
                await resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
                return;
            }

            var connectionString = await target.ConnectionStringExpression.GetValueAsync(cancellationToken);
            if (connectionString is null)
            {
                logger.LogError("Failed to retrieve connection string for target database {TargetDatabaseResourceName}.", target.Name);
                await resourceNotificationService.PublishUpdateAsync(sqlProject,
                    state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
                return;
            }

            await resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot("Publishing", KnownResourceStateStyles.Info) });

            deployer.Deploy(dacpacPath, connectionString, target.DatabaseName, logger, cancellationToken);

            await resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Finished, KnownResourceStateStyles.Success) });

            await eventing.PublishAsync(new ResourceReadyEvent(sqlProject, serviceProvider), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish database project.");

            await resourceNotificationService.PublishUpdateAsync(sqlProject,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
        }
    }
}
