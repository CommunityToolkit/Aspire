using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal class SqlProjectPublishService(IDacpacDeployer deployer, IDacpacChecksumService deploySkipper, IHostEnvironment hostEnvironment, ResourceLoggerService resourceLoggerService, ResourceNotificationService resourceNotificationService, IDistributedApplicationEventing eventing, IServiceProvider serviceProvider)
{
    public async Task PublishSqlProject(IResourceWithDacpac resource, IResourceWithConnectionString target, string? targetDatabaseName, CancellationToken cancellationToken)
    {
        var logger = resourceLoggerService.GetLogger(resource);

        try
        {
            var dacpacPath = resource.GetDacpacPath();
            if (!Path.IsPathRooted(dacpacPath))
            {
                dacpacPath = Path.Combine(hostEnvironment.ContentRootPath, dacpacPath);
            }

            if (!File.Exists(dacpacPath))
            {
                logger.LogError("SQL Server Database project package not found at path {DacpacPath}.", dacpacPath);
                await resourceNotificationService.PublishUpdateAsync(resource,
                    state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
                return;
            }
            else
            {
                logger.LogInformation("SQL Server Database project package found at path {DacpacPath}.", dacpacPath);
            }

            var options = resource.GetDacpacDeployOptions();

            var connectionString = await target.ConnectionStringExpression.GetValueAsync(cancellationToken);
            if (connectionString is null)
            {
                logger.LogError("Failed to retrieve connection string for target database {TargetDatabaseResourceName}.", target.Name);
                await resourceNotificationService.PublishUpdateAsync(resource,
                    state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
                return;
            }

            string? checksum = null;

            if (resource.HasAnnotationOfType<DacpacSkipWhenDeployedAnnotation>())
            {
                options.DropExtendedPropertiesNotInSource = false;

                var result = await deploySkipper.CheckIfDeployedAsync(dacpacPath, connectionString, logger, cancellationToken);
                if (string.IsNullOrEmpty(result))
                {
                    await resourceNotificationService.PublishUpdateAsync(resource,
                        state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Finished, KnownResourceStateStyles.Success) });
                    return;
                }

                checksum = result;
            }

            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with { State = new ResourceStateSnapshot("Publishing", KnownResourceStateStyles.Info) });

            deployer.Deploy(dacpacPath, options, connectionString, targetDatabaseName, logger, cancellationToken);

            if (!string.IsNullOrEmpty(checksum) && resource.HasAnnotationOfType<DacpacSkipWhenDeployedAnnotation>())
            {
                await deploySkipper.SetChecksumAsync(dacpacPath, connectionString, checksum, logger, cancellationToken);
            }

            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Finished, KnownResourceStateStyles.Success) });

            await eventing.PublishAsync(new ResourceReadyEvent(resource, serviceProvider), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish database project.");

            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error) });
        }
    }
}
