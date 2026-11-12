using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Xml.Linq;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal sealed class SqlProjectDacpacResolver(IHostEnvironment hostEnvironment)
{
    public async Task<ResolvedDacpac> ResolveAsync(IResourceWithDacpac resource, ILogger logger, CancellationToken cancellationToken)
    {
        string? temporaryDacpacDirectory = null;
        try
        {
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
            return new ResolvedDacpac(dacpacPath, dacpacIdentity, temporaryDacpacDirectory, logger);
        }
        catch
        {
            DeleteTemporaryDacpacDirectory(temporaryDacpacDirectory, logger);
            throw;
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

    private static async Task<string> RestoreNuGetPackageAsync(SqlPackageRestoreAnnotation packageAnnotation, ILogger logger, CancellationToken cancellationToken)
    {
        var temporaryDirectory = Path.Combine(packageAnnotation.AppHostDirectory, "obj", "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var projectPath = Path.Combine(temporaryDirectory, "PackageDownload.csproj");
            var project = CreatePackageDownloadProject(packageAnnotation, AppContext.TargetFrameworkName);
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

            var packageVersion = NuGetVersion.Parse(packageAnnotation.Version);
            var packageDirectory = assets.RootElement
                .GetProperty("packageFolders")
                .EnumerateObject()
                .Select(folder => new VersionFolderPathResolver(folder.Name, isLowercase: true).GetInstallPath(packageAnnotation.PackageId, packageVersion))
                .FirstOrDefault(Directory.Exists);

            if (packageDirectory is null)
            {
                throw new InvalidOperationException($"NuGet restore did not install package '{packageAnnotation.PackageId}' version '{packageAnnotation.Version}' in a reported package folder.");
            }

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

    internal static XDocument CreatePackageDownloadProject(SqlPackageRestoreAnnotation packageAnnotation, string? targetFrameworkName)
    {
        if (string.IsNullOrEmpty(targetFrameworkName))
        {
            throw new InvalidOperationException("The AppHost target framework is unavailable; cannot restore the NuGet package.");
        }

        var framework = new FrameworkName(targetFrameworkName);
        if (framework.Identifier != ".NETCoreApp" || framework.Version.Major < 5)
        {
            throw new InvalidOperationException($"The AppHost target framework '{targetFrameworkName}' is not supported for NuGet package restore.");
        }

        var targetFramework = $"net{framework.Version.Major}.{framework.Version.Minor}";
        return new XDocument(
            new XElement("Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("TargetFramework", targetFramework),
                    new XElement("DisableImplicitFrameworkReferences", "true")),
                new XElement("ItemGroup",
                    new XElement("PackageDownload",
                        new XAttribute("Include", packageAnnotation.PackageId),
                        new XAttribute("Version", $"[{packageAnnotation.Version}]")))));
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

    internal static void DeleteTemporaryDacpacDirectory(string? directory, ILogger logger)
    {
        if (directory is null)
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to remove temporary DACPAC directory {TemporaryDacpacDirectory}.", directory);
        }
    }
}

internal sealed class ResolvedDacpac(string path, string identity, string? temporaryDirectory, ILogger logger) : IDisposable
{
    public string Path { get; } = path;

    public string Identity { get; } = identity;

    public void Dispose() => SqlProjectDacpacResolver.DeleteTemporaryDacpacDirectory(temporaryDirectory, logger);
}
