using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Management.Automation;

namespace CommunityToolkit.Aspire.Hosting.PowerShell
{
    /// <summary>
    /// Represents a PowerShell script resource.
    /// </summary>
    public class PowerShellScriptResource : Resource, IDisposable,
        IResourceWithEnvironment,
        IResourceWithWaitSupport,
        IResourceWithArgs
    {
        private readonly System.Management.Automation.PowerShell _ps;
        private readonly CancellationTokenSource _cts;
        private readonly PowerShellRunspacePoolResource _parent;
        private readonly PSDataCollection<PSObject> _output;
        private readonly PSDataCollection<PSObject> _emptyInput;
        private ILogger? _scriptLogger;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellScriptResource"/> class, representing a resource for
        /// executing PowerShell scripts.
        /// </summary>
        /// <remarks>This class is designed to manage the execution of PowerShell scripts within a
        /// specified runspace pool. The script execution is tied to a cancellation token, allowing the script to be
        /// stopped when requested.</remarks>
        /// <param name="name">The name of the resource. This must be a valid resource name.</param>
        /// <param name="script">The ScriptBlock to be executed.</param>
        /// <param name="parent">The parent <see cref="PowerShellRunspacePoolResource"/> that provides the runspace pool for script
        /// execution. Cannot be null.</param>         
        public PowerShellScriptResource([ResourceName] string name,
            ScriptBlock script,
            PowerShellRunspacePoolResource parent) : base(name)
        {
            _parent = parent;
            _cts = new CancellationTokenSource();
            _ps = System.Management.Automation.PowerShell.Create();

            // stop the powershell instance when _cts is cancelled
            _cts.Token.Register(() =>
            {
                _scriptLogger?.LogInformation("Stopping PowerShell script execution");
                _ps.Stop();
            });

            _output = new PSDataCollection<PSObject>();
            _emptyInput = new PSDataCollection<PSObject>();
            _emptyInput.Complete();

            _ps.AddScript(script.ToString());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellScriptResource"/> class, representing a resource for
        /// executing PowerShell scripts.
        /// </summary>
        /// <remarks>This class is designed to manage the execution of PowerShell scripts within a
        /// specified runspace pool. The script execution is tied to a cancellation token, allowing the script to be
        /// stopped when requested.</remarks>
        /// <param name="name">The name of the resource. This must be a valid resource name.</param>
        /// <param name="scriptFile">The file containing the PowerShell script to be executed. Must be a valid file path and not null.</param>
        /// <param name="parent">The parent <see cref="PowerShellRunspacePoolResource"/> that provides the runspace pool for script
        /// execution. Cannot be null.</param>
        public PowerShellScriptResource([ResourceName] string name,
            FileInfo scriptFile,
            PowerShellRunspacePoolResource parent) : base(name)
        {
            _parent = parent;
            _cts = new CancellationTokenSource();
            _ps = System.Management.Automation.PowerShell.Create();

            // stop the powershell instance when _cts is cancelled
            _cts.Token.Register(() =>
            {
                _scriptLogger?.LogInformation("Stopping PowerShell script execution");
                _ps.Stop();
            });

            _output = new PSDataCollection<PSObject>();
            _emptyInput = new PSDataCollection<PSObject>();
            _emptyInput.Complete();

            // add scriptFile to the PowerShell instance to be executed
            _ps.AddCommand(scriptFile.FullName);
        }

        /// <summary>
        /// Breaks the PowerShell script execution.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> BreakAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PowerShellScriptResource));
            }

            if (_ps.InvocationStateInfo.State != PSInvocationState.Running)
            {
                return false;
            }

            await _cts.CancelAsync();

            return true;
        }

        /// <summary>
        /// Starts the PowerShell script execution.
        /// </summary>
        /// <param name="scriptLogger"></param>
        /// <param name="notificationService"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public async Task StartAsync(ILogger scriptLogger,
            ResourceNotificationService notificationService,
            CancellationToken cancellationToken = default)
        {
            Debug.Assert(scriptLogger != null);
            _scriptLogger = scriptLogger;

            Debug.Assert(_parent.Pool != null);
            _ps.RunspacePool = _parent.Pool;

            ConfigurePSDataStreams(scriptLogger, notificationService);

            _ps.InvocationStateChanged += async (_, args) =>
            {
                var knownState = args.InvocationStateInfo.State switch
                {
                    PSInvocationState.NotStarted => KnownResourceStates.NotStarted,
                    PSInvocationState.Running => KnownResourceStates.Running,
                    PSInvocationState.Completed => KnownResourceStates.Finished,
                    PSInvocationState.Stopped => KnownResourceStates.Exited,
                    PSInvocationState.Failed => KnownResourceStates.FailedToStart,
                    PSInvocationState.Stopping => KnownResourceStates.Stopping,
                    _ => throw new ArgumentOutOfRangeException( // probably should be assertion
                        nameof(args.InvocationStateInfo.State),
                        args.InvocationStateInfo.State,
                        "Unknown PowerShell invocation state")
                };

                await notificationService.PublishUpdateAsync(this,
                    state =>
                    {
                        state = state with
                        {
                            State = knownState,
                            Properties = [
                                .. state.Properties,
                                new( "PSInvocationState", args.InvocationStateInfo.State.ToString() ),
                                new( "Reason", args.InvocationStateInfo.Reason?.Message ?? string.Empty ),
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

            if (this.TryGetLastAnnotation<PowerShellScriptArgsAnnotation>(out var scriptArgsAnnotation))
            {
                foreach (var scriptArg in scriptArgsAnnotation.Args)
                {
                    if (scriptArg is IValueProvider valueProvider)
                    {
                        var value = await valueProvider.GetValueAsync(cancellationToken);
                        _ps.AddArgument(value);
                    }
                    else
                    {
                        _ps.AddArgument(scriptArg);
                    }
                }
            }

            try
            {
                _ = await _ps.InvokeAsync(_emptyInput, _output);
            }
            catch (PipelineStoppedException)
            {
                // This is expected when the pipeline is stopped (i.e. via BreakAsync), so we can ignore it.
                // The pipeline will be stopped when the PowerShell instance is disposed.
            }
            catch (Exception ex)
            {
                scriptLogger.LogError(ex, "Error invoking PowerShell script: {Message}", ex.Message);
                throw;
            }
        }

        private void ConfigurePSDataStreams(ILogger logger, ResourceNotificationService notifications)
        {
            _output.DataAdded += (_, args) =>
            {
                var psObject = _output[args.Index];
                logger.LogInformation("Output: {Output}", psObject.ToString());
            };
            _output.Completed += async (_, _) =>
            {
                await notifications.PublishUpdateAsync(this, state => state with
                {
                    State = KnownResourceStates.Finished,
                    StopTimeStamp = DateTime.Now,
                    ExitCode = _ps.HadErrors ? 1 : 0,
                });

                logger.LogInformation("Output completed");
            };
            _ps.Streams.Error.DataAdded += (_, args) =>
            {
                var error = _ps.Streams.Error[args.Index];
                logger.LogError(error.Exception, "Error: {Error}", error.ToString());
            };
            _ps.Streams.Warning.DataAdded += (_, args) =>
            {
                var warning = _ps.Streams.Warning[args.Index];
                logger.LogWarning("Warning: {Warning}", warning);
            };
            _ps.Streams.Information.DataAdded += (_, args) =>
            {
                var info = _ps.Streams.Information[args.Index];
                logger.LogInformation("Information: {Info}", info);
            };
            _ps.Streams.Verbose.DataAdded += (_, args) =>
            {
                var verbose = _ps.Streams.Verbose[args.Index];
                logger.LogInformation("Verbose: {Verbose}", verbose);
            };
            _ps.Streams.Debug.DataAdded += (_, args) =>
            {
                var debug = _ps.Streams.Debug[args.Index];
                logger.LogInformation("Debug: {Debug}", debug);
            };
        }

        void IDisposable.Dispose()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PowerShellScriptResource));
            }
            _isDisposed = true;
            _ps.Stop();
            _ps.Dispose();
        }
    }
}
