using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal class SqlProjectPublishService(IDacpacDeployer deployer, IDacpacChecksumService dacpacChecksumService, IHostEnvironment hostEnvironment, ResourceLoggerService resourceLoggerService, ResourceNotificationService resourceNotificationService)
{
    public async Task PublishSqlProject(IResourceWithDacpac resource, IResourceWithConnectionString target, string? targetDatabaseName, CancellationToken cancellationToken)
    {
        var logger = resourceLoggerService.GetLogger(resource);
        ResourceStateSnapshot? failureState = KnownResourceStates.FailedToStart;

        try
        {
            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Starting, KnownResourceStateStyles.Error) });

            if (resource.TryGetLastAnnotation<SqlProjectBuildAnnotation>(out var buildAnnotation)
                && resource.TryGetLastAnnotation<IProjectMetadata>(out var projectMetadata))
            {
                await buildAnnotation.BuildLock.WaitAsync(cancellationToken);
                try
                {
                    await BuildProjectAsync(projectMetadata.ProjectPath, buildAnnotation.Configuration, logger, cancellationToken);
                }
                finally
                {
                    buildAnnotation.BuildLock.Release();
                }
            }

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
                    state => state with { State = KnownResourceStates.FailedToStart });
                return;
            }

            failureState = KnownResourceStates.Finished;
            string? checksum = null;

            if (resource.HasAnnotationOfType<DacpacSkipWhenDeployedAnnotation>())
            {
                options.DropExtendedPropertiesNotInSource = false;

                var result = await dacpacChecksumService.CheckIfDeployedAsync(dacpacPath, connectionString, logger, cancellationToken);
                if (result is null)
                {
                    await resourceNotificationService.PublishUpdateAsync(resource,
                        state => state with { State = KnownResourceStates.Finished });
                    return;
                }

                checksum = result;
            }

            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with
                {
                    State = KnownResourceStates.Running,
                    StartTimeStamp = DateTime.UtcNow
                });

            deployer.Deploy(dacpacPath, options, connectionString, targetDatabaseName, logger, cancellationToken);

            if (!string.IsNullOrEmpty(checksum) && resource.HasAnnotationOfType<DacpacSkipWhenDeployedAnnotation>())
            {
                await dacpacChecksumService.SetChecksumAsync(dacpacPath, connectionString, checksum, logger, cancellationToken);
            }

            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with
                {
                    State = KnownResourceStates.Finished,
                    ExitCode = 0,
                    StopTimeStamp = DateTime.UtcNow
                });

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish database project.");

            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with
                {
                    State = failureState,
                    ExitCode = failureState == KnownResourceStates.Finished ? 1 : state.ExitCode,
                    StopTimeStamp = DateTime.UtcNow
                });
        }
    }

    private static async Task BuildProjectAsync(string projectPath, string configuration, ILogger logger, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        processStartInfo.ArgumentList.Add("build");
        processStartInfo.ArgumentList.Add(projectPath);
        processStartInfo.ArgumentList.Add("--configuration");
        processStartInfo.ArgumentList.Add(configuration);

        logger.LogInformation("Building SQL Server Database project {ProjectPath} with configuration {Configuration}.", projectPath, configuration);

        using var process = new Process { StartInfo = processStartInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start the dotnet build process.");
        }

        var standardOutputTask = LogBuildOutputAsync(process.StandardOutput, logger);
        var standardErrorTask = LogBuildOutputAsync(process.StandardError, logger);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            await Task.WhenAll(standardOutputTask, standardErrorTask);
            throw;
        }

        await Task.WhenAll(standardOutputTask, standardErrorTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Building SQL Server Database project '{projectPath}' failed with exit code {process.ExitCode}.");
        }
    }

    private static async Task LogBuildOutputAsync(StreamReader streamReader, ILogger logger)
    {
        while (await streamReader.ReadLineAsync() is { } outputLine)
        {
            logger.LogInformation("SQL project build: {BuildOutput}", outputLine);
        }
    }
}
