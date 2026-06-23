using System.Runtime.CompilerServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;

#pragma warning disable ASPIREINTERACTION001
internal static class ParameterResourceExtensions
{
    // Registered by the provisioner at startup (AuthenticateAsync) so that compatibility-break
    // warnings have somewhere to go. Defaults to NullLogger so the code never crashes on logging.
    private static ILogger s_logger = NullLogger.Instance;

    // Called once by BitwardenSecretManagerProvisioner.AuthenticateAsync, which is the first
    // provisioner method invoked in both run mode and publish mode.
    internal static void SetCompatibilityLogger(ILogger logger)
    {
        // Best-effort; a race between two provisioner instances is harmless.
        if (s_logger is NullLogger)
        {
            s_logger = logger;
        }
    }

    extension(ParameterResource parameter)
    {
        public bool HasValue()
        {
            // Messy but there is no obvious better way to synchronously check if the parameter has a value
            try
            {
                var tcs = GetWaitForValueTcs(parameter);
                if (tcs is not null)
                {
                    return tcs.Task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(tcs.Task.Result);
                }

                // No TCS means GetValueAsync delegates synchronously to ValueInternal (the lazy
                // Func<string> valueGetter). Check IsCompleted before calling GetResult per ValueTask rules.
                try
                {
                    var valueTask = parameter.GetValueAsync(CancellationToken.None);
                    string? value = valueTask.IsCompleted ? valueTask.GetAwaiter().GetResult() : null;
                    return !string.IsNullOrWhiteSpace(value);
                }
                catch (MissingParameterValueException)
                {
                    return false;
                }
            }
            catch (MissingMemberException ex)
            {
                WarnCompatibilityBreak(ex, "ParameterResource.WaitForValueTcs (getter)");
                return false;
            }
        }

        public async ValueTask PromptAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(services);

            ParameterProcessor parameterProcessor = services.GetRequiredService<ParameterProcessor>();
            await parameterProcessor.SetParameterAsync(parameter, cancellationToken).ConfigureAwait(false);
        }

        // Called by the Bitwarden provisioner after it resolves a secret value from the remote.
        // Resolves the WaitForValueTcs so callers awaiting GetValueAsync() unblock immediately.
        public void ResolveWaitForValue(string resolvedValue)
        {
            try
            {
                var tcs = GetWaitForValueTcs(parameter);
                // Only set if pending; don't overwrite a value the user already provided via the dashboard.
                if (tcs is not null && !tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(resolvedValue);
                }
            }
            catch (MissingMemberException ex)
            {
                WarnCompatibilityBreak(ex, "ParameterResource.WaitForValueTcs (getter)");
            }
        }

        // Creates a WaitForValueTcs on the parameter so that a subsequent PromptAsync call can store
        // the entered value before ParameterProcessor.InitializeParametersAsync runs. Without this,
        // TrySetResult in ApplyParameterValueAsync is a no-op (TCS is null) and the entered value
        // is lost. Call immediately before PromptAsync; retrieve the result with GetResolvedWaitForValue.
        internal void InitializeWaitForValue()
        {
            try
            {
                SetWaitForValueTcs(parameter, new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously));
            }
            catch (MissingMemberException ex)
            {
                WarnCompatibilityBreak(ex, "ParameterResource.WaitForValueTcs (setter)");
            }
        }

        // Returns the value stored by PromptAsync after InitializeWaitForValue was called,
        // or null if the prompt was cancelled, the TCS is not yet completed, or the accessor broke.
        internal string? GetResolvedWaitForValue()
        {
            try
            {
                var tcs = GetWaitForValueTcs(parameter);
                return tcs?.Task is { IsCompletedSuccessfully: true } t ? t.Result : null;
            }
            catch (MissingMemberException ex)
            {
                WarnCompatibilityBreak(ex, "ParameterResource.WaitForValueTcs (getter)");
                return null;
            }
        }
    }

    // Removes a resolved parameter from ParameterProcessor's pending list and cancels the banner
    // if all parameters are now satisfied. This prevents the "parameters need values" prompt from
    // lingering after Bitwarden has already provided the value.
    internal static void MarkParameterResolved(ParameterProcessor parameterProcessor, ParameterResource parameter)
    {
        try
        {
            ref List<ParameterResource> unresolved = ref GetUnresolvedParameters(parameterProcessor);
            unresolved.Remove(parameter);

            if (unresolved.Count == 0)
            {
                GetAllParametersResolvedCts(parameterProcessor)?.Cancel();
            }
        }
        catch (MissingMemberException ex)
        {
            WarnCompatibilityBreak(ex, "ParameterProcessor._unresolvedParameters / _allParametersResolvedCts");
        }
    }

    private static void WarnCompatibilityBreak(MissingMemberException ex, string member) =>
        s_logger.LogWarning(ex,
            "Aspire internal member '{Member}' is no longer accessible. " +
            "The Bitwarden integration may behave incorrectly with this version of Aspire. " +
            "See ASPIRE-INTERNALS.md for upgrade guidance.",
            member);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_WaitForValueTcs")]
    static extern TaskCompletionSource<string>? GetWaitForValueTcs(ParameterResource parameter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_WaitForValueTcs")]
    static extern void SetWaitForValueTcs(ParameterResource parameter, TaskCompletionSource<string>? value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_unresolvedParameters")]
    static extern ref List<ParameterResource> GetUnresolvedParameters(ParameterProcessor processor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allParametersResolvedCts")]
    static extern ref CancellationTokenSource? GetAllParametersResolvedCts(ParameterProcessor processor);
}
#pragma warning restore ASPIREINTERACTION001
