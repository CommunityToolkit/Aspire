using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace CommunityToolkit.Aspire.Hosting.PowerShell;

/// <summary>
/// Represents a PowerShell runspace pool resource.
/// </summary>
public class PowerShellRunspacePoolResource(
    [ResourceName] string name,
    PSLanguageMode languageMode = PSLanguageMode.ConstrainedLanguage,
    int minRunspaces = 1,
    int maxRunspaces = 5)
    : Resource(name), IDisposable, IResourceWithWaitSupport
{
    /// <summary>
    /// Specifies the language mode for the PowerShell runspace pool.
    /// </summary>
    public PSLanguageMode LanguageMode { get; } = languageMode;

    /// <summary>
    /// Specifies the minimum number of runspaces in the pool.
    /// </summary>
    public int MinRunspaces { get; } = minRunspaces;

    /// <summary>
    /// Specifies the maximum number of runspaces in the pool.
    /// </summary>
    public int MaxRunspaces { get; } = maxRunspaces;

    /// <summary>
    /// A reference to the runspace pool created by this resource.
    /// </summary>
    public RunspacePool? Pool { get; private set; }

    internal Task StartAsync(InitialSessionState sessionState, ResourceNotificationService notificationService, ILogger logger, CancellationToken token = default)
    {
        sessionState.LanguageMode = this.LanguageMode;
        sessionState.AuthorizationManager = new AuthorizationManager("Aspire");
        Pool = RunspaceFactory.CreateRunspacePool(MinRunspaces, MaxRunspaces, sessionState, new AspirePSHost(logger));

        ConfigureStateChangeNotifications(notificationService, logger);

        return Task.Factory.FromAsync(Pool.BeginOpen, Pool.EndOpen, null);
    }

    private void ConfigureStateChangeNotifications(ResourceNotificationService notificationService, ILogger logger)
    {
        Pool!.StateChanged += async (_, args) =>
        {
            var poolState = args.RunspacePoolStateInfo.State;
            var reason = args.RunspacePoolStateInfo.Reason;

            logger.LogInformation(
                "Runspace pool '{PoolName}' state changed to '{RunspacePoolState}'", Name, poolState);

            // map args.RunspacePoolStateInfo.State to a KnownResourceState
            // and publish the update

            var knownState = poolState switch
            {
                RunspacePoolState.BeforeOpen => KnownResourceStates.NotStarted,
                RunspacePoolState.Opening => KnownResourceStates.Starting,
                RunspacePoolState.Opened => KnownResourceStates.Running,
                RunspacePoolState.Closing => KnownResourceStates.Stopping,
                RunspacePoolState.Closed => KnownResourceStates.Exited,
                RunspacePoolState.Broken => KnownResourceStates.FailedToStart,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(poolState), poolState, $"Unexpected runspace pool state {poolState}")
            };

            await notificationService.PublishUpdateAsync(this,
                state => {
                    state = state with
                    {
                        State = knownState,
                        Properties = [.. state.Properties,
                        new("RunspacePoolState", poolState.ToString()),
                        new("Reason", reason?.ToString() ?? string.Empty)
                        ]
                    };

                    if (knownState == KnownResourceStates.Running)
                    {
                        state = state with
                        {
                            StartTimeStamp = DateTime.Now,
                        };
                    }

                    if (KnownResourceStates.TerminalStates.Contains(knownState))
                    {
                        state = state with
                        {
                            StopTimeStamp = DateTime.Now,
                        };
                    }

                    return state;
                });
        };
    }

    // absolutely brain dead and deficient (minimal) PSHost implementation
    private class AspirePSHost(ILogger logger) : PSHost
    {
        public override void SetShouldExit(int exitCode)
        {
            logger.LogInformation("AspirePSHost: SetShouldExit({ExitCode})", exitCode);
        }

        public override void EnterNestedPrompt()
        {
            throw new NotSupportedException();
        }

        public override void ExitNestedPrompt()
        {
            throw new NotSupportedException();
        }

        public override void NotifyBeginApplication()
        {
            logger.LogInformation("AspirePSHost: NotifyBeginApplication");
        }

        public override void NotifyEndApplication()
        {
            logger.LogInformation("AspirePSHost: NotifyEndApplication");
        }

        public override string Name { get; } = "AspirePSHost";
        public override Version Version { get; } = new (0, 1);
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override PSHostUserInterface UI => null!; // interaction not supported
        public override CultureInfo CurrentCulture { get; } = CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture { get; } = CultureInfo.CurrentUICulture;
    }

    void IDisposable.Dispose()
    {
        Pool?.Close();
        Pool?.Dispose();
    }
}