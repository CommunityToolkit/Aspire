using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal class SqlProjectPublishService(IDacpacDeployer deployer, IDacpacChecksumService dacpacChecksumService, IHostEnvironment hostEnvironment, ResourceLoggerService resourceLoggerService, ResourceNotificationService resourceNotificationService)
{
    public async Task PublishSqlProject(IResourceWithDacpac resource, IResourceWithConnectionString target, string? targetDatabaseName, CancellationToken cancellationToken)
    {
        var logger = resourceLoggerService.GetLogger(resource);
        ResourceStateSnapshot? failureState = KnownResourceStates.FailedToStart;
        string? temporaryDacpacDirectory = null;

        try
        {
            await resourceNotificationService.PublishUpdateAsync(resource,
                state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Starting, KnownResourceStateStyles.Error) });

            string? dacpacPath = null;
            string? dacpacIdentity = null;
            string? projectDirectory = null;
            if (resource.TryGetLastAnnotation<IProjectMetadata>(out var metadata))
            {
                projectDirectory = Path.GetDirectoryName(Path.GetFullPath(metadata.ProjectPath, hostEnvironment.ContentRootPath));
            }

            if (resource.TryGetLastAnnotation<SqlProjectBuildAnnotation>(out var buildAnnotation)
                && resource.TryGetLastAnnotation<IProjectMetadata>(out var projectMetadata))
            {
                var buildLock = SqlProjectBuildLock.Get(projectMetadata.ProjectPath);
                await buildLock.WaitAsync(cancellationToken);
                try
                {
                    await BuildProjectAsync(projectMetadata.ProjectPath, buildAnnotation.Configuration, logger, cancellationToken);
                    var builtDacpacPath = ResolveDacpacPath(resource.GetDacpacPath(), projectDirectory);
                    dacpacIdentity = builtDacpacPath;
                    if (File.Exists(builtDacpacPath))
                    {
                        temporaryDacpacDirectory = Path.Combine(Path.GetTempPath(), "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects", Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(temporaryDacpacDirectory);
                        dacpacPath = Path.Combine(temporaryDacpacDirectory, Path.GetFileName(builtDacpacPath));
                        File.Copy(builtDacpacPath, dacpacPath);
                    }
                    else
                    {
                        dacpacPath = builtDacpacPath;
                    }
                }
                finally
                {
                    buildLock.Release();
                }
            }

            if (resource.TryGetLastAnnotation<SqlPackageRestoreAnnotation>(out var packageAnnotation))
            {
                await packageAnnotation.RestoreLock.WaitAsync(cancellationToken);
                try
                {
                    dacpacPath = await RestoreNuGetPackageAsync(packageAnnotation, logger, cancellationToken);
                    dacpacIdentity = dacpacPath;
                    resource.Annotations.Add(new DacpacMetadataAnnotation(dacpacPath));
                }
                finally
                {
                    packageAnnotation.RestoreLock.Release();
                }
            }

            dacpacPath ??= ResolveDacpacPath(resource.GetDacpacPath(), projectDirectory);
            dacpacIdentity ??= dacpacPath;

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

                var result = await dacpacChecksumService.CheckIfDeployedAsync(dacpacPath, dacpacIdentity, connectionString, logger, cancellationToken);
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
                await dacpacChecksumService.SetChecksumAsync(dacpacPath, dacpacIdentity, connectionString, checksum, logger, cancellationToken);
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
        finally
        {
            if (temporaryDacpacDirectory is not null)
            {
                try
                {
                    Directory.Delete(temporaryDacpacDirectory, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Unable to remove temporary DACPAC directory {TemporaryDacpacDirectory}.", temporaryDacpacDirectory);
                }
            }
        }
    }

    private string ResolveDacpacPath(string dacpacPath, string? baseDirectory = null)
        => Path.IsPathRooted(dacpacPath)
            ? dacpacPath
            : Path.GetFullPath(dacpacPath, baseDirectory ?? hostEnvironment.ContentRootPath);

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

        await RunProcessAsync(processStartInfo, $"Building SQL Server Database project '{projectPath}' with configuration '{configuration}'", logger, cancellationToken);
    }

    internal static async Task<string> RestoreNuGetPackageAsync(SqlPackageRestoreAnnotation packageAnnotation, ILogger logger, CancellationToken cancellationToken)
    {
        var temporaryDirectory = Path.Combine(packageAnnotation.AppHostDirectory, "obj", "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var projectPath = Path.Combine(temporaryDirectory, "PackageDownload.csproj");
            var project = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", "net8.0"),
                        new XElement("DisableImplicitFrameworkReferences", "true")),
                    new XElement("ItemGroup",
                        new XElement("PackageDownload",
                            new XAttribute("Include", packageAnnotation.PackageId),
                            new XAttribute("Version", $"[{packageAnnotation.Version}]")))));
            await File.WriteAllTextAsync(projectPath, project.ToString(SaveOptions.DisableFormatting), cancellationToken);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = packageAnnotation.AppHostDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            processStartInfo.ArgumentList.Add("restore");
            processStartInfo.ArgumentList.Add(projectPath);
            processStartInfo.ArgumentList.Add("--verbosity");
            processStartInfo.ArgumentList.Add("minimal");
            processStartInfo.ArgumentList.Add("--property:ImportDirectoryBuildProps=false");
            processStartInfo.ArgumentList.Add("--property:ImportDirectoryBuildTargets=false");
            processStartInfo.ArgumentList.Add("--property:ManagePackageVersionsCentrally=false");
            processStartInfo.ArgumentList.Add("--property:ImportDirectoryPackagesProps=false");

            await RunProcessAsync(processStartInfo, $"Restoring NuGet package '{packageAnnotation.PackageId}' version '{packageAnnotation.Version}'", logger, cancellationToken);

            var assetsPath = Path.Combine(temporaryDirectory, "obj", "project.assets.json");
            using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(assetsPath, cancellationToken));
            ValidateRestoredPackage(assets.RootElement, packageAnnotation);

            var cachePath = Path.Combine(temporaryDirectory, "obj", "project.nuget.cache");
            using var cache = JsonDocument.Parse(await File.ReadAllTextAsync(cachePath, cancellationToken));
            var expectedPackageFiles = cache.RootElement
                .GetProperty("expectedPackageFiles")
                .EnumerateArray()
                .Select(file => file.GetString())
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .ToArray();

            if (expectedPackageFiles.Length != 1 || !Path.IsPathRooted(expectedPackageFiles[0]!))
            {
                throw new InvalidOperationException($"NuGet restore did not report one absolute package path for '{packageAnnotation.PackageId}' version '{packageAnnotation.Version}'.");
            }

            var packageMetadataPath = expectedPackageFiles[0]!;
            if (!Path.GetFileName(packageMetadataPath).EndsWith(".nupkg.sha512", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"NuGet restore reported an unexpected package metadata file for '{packageAnnotation.PackageId}' version '{packageAnnotation.Version}'.");
            }

            var packageDirectory = Path.GetDirectoryName(packageMetadataPath)
                ?? throw new InvalidOperationException($"NuGet restore did not report a package directory for '{packageAnnotation.PackageId}' version '{packageAnnotation.Version}'.");
            var relativeDacpacPath = packageAnnotation.DacpacRelativePath ?? Path.Combine("tools", $"{packageAnnotation.PackageId}.dacpac");
            var dacpacPath = ResolvePackageFilePath(packageDirectory, relativeDacpacPath);

            if (!File.Exists(dacpacPath))
            {
                throw new FileNotFoundException($"DACPAC '{relativeDacpacPath}' was not found in NuGet package '{packageAnnotation.PackageId}' version '{packageAnnotation.Version}'.", dacpacPath);
            }

            logger.LogInformation("NuGet package {PackageId} version {PackageVersion} restored to {PackageDirectory}.", packageAnnotation.PackageId, packageAnnotation.Version, packageDirectory);
            return dacpacPath;
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Unable to remove temporary NuGet restore directory {TemporaryDirectory}.", temporaryDirectory);
            }
        }
    }

    private static void ValidateRestoredPackage(JsonElement assets, SqlPackageRestoreAnnotation packageAnnotation)
    {
        var frameworks = assets.GetProperty("project").GetProperty("frameworks");
        var downloadedPackages = frameworks.EnumerateObject()
            .SelectMany(framework => framework.Value.TryGetProperty("downloadDependencies", out var dependencies)
                ? dependencies.EnumerateArray().ToArray()
                : []);
        var expectedVersion = $"[{packageAnnotation.Version},{packageAnnotation.Version}]";
        var packageMatches = downloadedPackages.Count(package =>
            string.Equals(package.GetProperty("name").GetString(), packageAnnotation.PackageId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(package.GetProperty("version").GetString()?.Replace(" ", string.Empty, StringComparison.Ordinal), expectedVersion, StringComparison.OrdinalIgnoreCase));

        if (packageMatches != 1 || downloadedPackages.Count() != 1)
        {
            throw new InvalidOperationException($"NuGet restore did not resolve exactly package '{packageAnnotation.PackageId}' at version '{packageAnnotation.Version}'.");
        }
    }

    private static string ResolvePackageFilePath(string packageDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("The DACPAC path inside a NuGet package must be relative to the package root.");
        }

        var packageRoot = Path.GetFullPath(packageDirectory);
        var dacpacPath = Path.GetFullPath(relativePath, packageRoot);
        var packageRootPrefix = Path.EndsInDirectorySeparator(packageRoot)
            ? packageRoot
            : packageRoot + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!dacpacPath.StartsWith(packageRootPrefix, pathComparison))
        {
            throw new InvalidOperationException("The DACPAC path inside a NuGet package must remain within the package root.");
        }

        return dacpacPath;
    }

    private static async Task RunProcessAsync(ProcessStartInfo processStartInfo, string operation, ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("{Operation}.", operation);

        using var process = new Process { StartInfo = processStartInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Unable to start dotnet while {operation.ToLowerInvariant()}.");
        }

        var recentOutput = new ConcurrentQueue<string>();
        var standardOutputTask = LogBuildOutputAsync(process.StandardOutput, logger, recentOutput);
        var standardErrorTask = LogBuildOutputAsync(process.StandardError, logger, recentOutput);

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
            var diagnosticOutput = string.Join(Environment.NewLine, recentOutput);
            throw new InvalidOperationException($"{operation} failed with exit code {process.ExitCode}.{Environment.NewLine}{diagnosticOutput}");
        }
    }

    private static async Task LogBuildOutputAsync(StreamReader streamReader, ILogger logger, ConcurrentQueue<string>? recentOutput = null)
    {
        while (await streamReader.ReadLineAsync() is { } outputLine)
        {
            logger.LogInformation("SQL project build: {BuildOutput}", outputLine);
            if (recentOutput is not null)
            {
                recentOutput.Enqueue(outputLine);
                while (recentOutput.Count > 20)
                {
                    recentOutput.TryDequeue(out _);
                }
            }
        }
    }
}
