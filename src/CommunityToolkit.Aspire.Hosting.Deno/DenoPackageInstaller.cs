using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CommunityToolkit.Aspire.Hosting.Deno;

/// <summary>
/// Represents a Deno package installer.
/// </summary>
/// <param name="packageManager">The package manager to use.</param>
/// <param name="loggerService">The logger service to use.</param>
/// <param name="notificationService">The notification service to use.</param>
internal class DenoPackageInstaller(string packageManager, ResourceLoggerService loggerService, ResourceNotificationService notificationService)
{
    private readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Finds the Deno resources using the specified package manager and installs the packages.
    /// </summary>
    /// <param name="appModel">The current AppHost instance.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task InstallPackages(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        var denoResources = appModel.Resources.OfType<DenoAppResource>();

        var packageResources = denoResources.Where(n => n.Command == packageManager);

        foreach (var resource in packageResources)
        {
            await PerformInstall(resource, cancellationToken);
        }
    }

    /// <summary>
    /// Performs the installation of packages for the specified Deno app resource in a background task and sends notifications to the AppHost.
    /// </summary>
    /// <param name="resource">The Deno application resource to install packages for.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException">Thrown if there is no package.json file or the package manager exits with a non-successful error code.</exception>
    private async Task PerformInstall(DenoAppResource resource, CancellationToken cancellationToken)
    {
        var logger = loggerService.GetLogger(resource);

        var packageJsonPath = Path.Combine(resource.WorkingDirectory, "deno.lock");

        if (!File.Exists(packageJsonPath))
        {
            await notificationService.PublishUpdateAsync(resource, state => state with
            {
                State = new($"No deno.lock file found in {resource.WorkingDirectory}", KnownResourceStates.FailedToStart)
            }).ConfigureAwait(false);

            throw new InvalidOperationException($"No deno.lock file found in {resource.WorkingDirectory}");
        }

        await notificationService.PublishUpdateAsync(resource, state => state with
        {
            State = new($"Installing {packageManager} packages in {resource.WorkingDirectory}", KnownResourceStates.Starting)
        }).ConfigureAwait(false);

        logger.LogInformation("Installing {PackageManager} packages in {WorkingDirectory}", packageManager, resource.WorkingDirectory);

        var packageInstaller = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd" : packageManager,
                Arguments = isWindows ? $"/c {packageManager} install" : "install",
                WorkingDirectory = resource.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false,
            }
        };

        packageInstaller.OutputDataReceived += async (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                await notificationService.PublishUpdateAsync(resource, state => state with
                {
                    State = new(args.Data, KnownResourceStates.Starting)
                }).ConfigureAwait(false);

                logger.LogInformation("{Data}", args.Data);
            }
        };

        packageInstaller.ErrorDataReceived += async (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                await notificationService.PublishUpdateAsync(resource, state => state with
                {
                    State = new(args.Data, KnownResourceStates.FailedToStart)
                }).ConfigureAwait(false);

                logger.LogError("{Data}", args.Data);
            }
        };

        packageInstaller.Start();
        packageInstaller.BeginOutputReadLine();
        packageInstaller.BeginErrorReadLine();

        await packageInstaller.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (packageInstaller.ExitCode != 0)
        {
            await notificationService.PublishUpdateAsync(resource, state => state with
            {
                State = new($"{packageManager} exited with {packageInstaller.ExitCode}", KnownResourceStates.FailedToStart)
            }).ConfigureAwait(false);

            throw new InvalidOperationException($"{packageManager} install failed with exit code {packageInstaller.ExitCode}");
        }
    }
}