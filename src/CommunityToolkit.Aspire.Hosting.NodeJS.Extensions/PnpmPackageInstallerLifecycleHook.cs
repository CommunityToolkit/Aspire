using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions;

/// <summary>
/// An <see cref="IDistributedApplicationLifecycleHook"/> that installs Node.js packages using the pnpm package manager before the Node.js resource starts.
/// </summary>
/// <param name="loggerService">The <see cref="ResourceLoggerService"/> to use for logging.</param>
/// <param name="notificationService">The <see cref="ResourceNotificationService"/> to use for notifications to Aspire on install progress.</param>
/// <param name="context">The <see cref="DistributedApplicationExecutionContext"/> to use for determining if the application is in publish mode.</param>
internal class PnpmPackageInstallerLifecycleHook(
    ResourceLoggerService loggerService,
    ResourceNotificationService notificationService,
    DistributedApplicationExecutionContext context) : IDistributedApplicationLifecycleHook
{
    private readonly NodePackageInstaller _installer = new("pnpm", "install", "pnpm-lock.yaml", loggerService, notificationService);

    /// <inheritdoc />
    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (context.IsPublishMode)
        {
            return Task.CompletedTask;
        }

        return _installer.InstallPackages(appModel, cancellationToken);
    }
}