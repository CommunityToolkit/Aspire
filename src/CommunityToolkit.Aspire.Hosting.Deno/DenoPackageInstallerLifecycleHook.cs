using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;

namespace CommunityToolkit.Aspire.Hosting.Deno;
/// <summary>
/// Represents a lifecycle hook for installing packages using npm as the package manager.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DenoPackageInstallerLifecycleHook"/> class with the specified logger service, notification service, and execution context.
/// </remarks>
/// <param name="loggerService">The logger service used for logging.</param>
/// <param name="notificationService">The notification service used for sending notifications.</param>
/// <param name="context">The execution context of the distributed application.</param>
internal class DenoPackageInstallerLifecycleHook(
    ResourceLoggerService loggerService,
    ResourceNotificationService notificationService,
    DistributedApplicationExecutionContext context) : IDistributedApplicationLifecycleHook
{
    private readonly DenoPackageInstaller _installer = new("deno", "install", "deno.lock", loggerService, notificationService);

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