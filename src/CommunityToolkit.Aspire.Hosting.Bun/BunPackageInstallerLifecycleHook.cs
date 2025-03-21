using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CommunityToolkit.Aspire.Hosting.Bun;

internal class BunPackageInstallerLifecycleHook(
    ResourceLoggerService loggerService,
    ResourceNotificationService notificationService,
    DistributedApplicationExecutionContext context) : IDistributedApplicationLifecycleHook
{
    private readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Performs the installation of packages for the specified Bun app resource in a background task and sends notifications to the AppHost.
    /// </summary>
    /// <param name="resource">The Bun application resource to install packages for.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException">Thrown if there is no package.json file or the package manager exits with a non-successful error code.</exception>
    private async Task PerformInstall(BunAppResource resource, CancellationToken cancellationToken)
    {
        var logger = loggerService.GetLogger(resource);

        // Bun v1.2 changed the default lockfile format to the text-based bun.lock. 
        // This code currently supports both formats, but will need to be updated in the future.
        var lockbFilePath = Path.Combine(resource.WorkingDirectory, "bun.lockb");
        var lockFilePath = Path.Combine(resource.WorkingDirectory, "bun.lock");

        // Bun supports workspaces in package.json (https://bun.sh/docs/install/workspaces)
        var packageJsonPath = Path.Combine(resource.WorkingDirectory, "package.json");

        string[] filePaths = [lockbFilePath, lockFilePath, packageJsonPath];

        if (!filePaths.Any(File.Exists))
        {
            await notificationService.PublishUpdateAsync(resource, state => state with
            {
                State = new($"No package manager file found in {resource.WorkingDirectory}", KnownResourceStates.FailedToStart)
            }).ConfigureAwait(false);

            throw new InvalidOperationException($"No package manager file found in {resource.WorkingDirectory}");
        }

        await notificationService.PublishUpdateAsync(resource, state => state with
        {
            State = new($"Installing bun packages in {resource.WorkingDirectory}", KnownResourceStates.Starting)
        }).ConfigureAwait(false);

        logger.LogInformation("Installing bun packages in {WorkingDirectory}", resource.WorkingDirectory);

        var packageInstaller = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd" : "bun",
                Arguments = isWindows ? "/c bun install" : "install",
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
                State = new($"bun exited with {packageInstaller.ExitCode}", KnownResourceStates.FailedToStart)
            }).ConfigureAwait(false);

            throw new InvalidOperationException($"bun install failed with exit code {packageInstaller.ExitCode}");
        }
    }

    /// <inheritdoc />
    public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (context.IsPublishMode)
        {
            return;
        }

        var bunResources = appModel.Resources.OfType<BunAppResource>();

        foreach (var resource in bunResources)
        {
            await PerformInstall(resource, cancellationToken);
        }
    }
}