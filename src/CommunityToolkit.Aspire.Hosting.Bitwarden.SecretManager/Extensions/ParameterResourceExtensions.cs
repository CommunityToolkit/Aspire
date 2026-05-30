using System.Runtime.CompilerServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;

#pragma warning disable ASPIREINTERACTION001
internal static class ParameterResourceExtensions
{
    extension(ParameterResource parameter)
    {
        public bool HasValue()
        {
            // Messy but there is no obvious better way to synchronously check if the parameter has a value
            var tcs = GetWaitForValueTcs(parameter);
            if (tcs is not null)
            {
                return tcs.Task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(tcs.Task.Result);
            }

            // No TCS means value comes from Func<string> synchronously, GetValueAsync won't block.
            try
            {
                string? value = parameter.GetValueAsync(CancellationToken.None).GetAwaiter().GetResult();
                return !string.IsNullOrWhiteSpace(value);
            }
            catch (MissingParameterValueException)
            {
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
            var tcs = GetWaitForValueTcs(parameter);
            // Only set if pending; don't overwrite a value the user already provided via the dashboard.
            if (tcs is not null && !tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(resolvedValue);
            }
        }
    }

    // Removes a resolved parameter from ParameterProcessor's pending list and cancels the banner
    // if all parameters are now satisfied. This prevents the "parameters need values" prompt from
    // lingering after Bitwarden has already provided the value.
    internal static void MarkParameterResolved(ParameterProcessor parameterProcessor, ParameterResource parameter)
    {
        ref List<ParameterResource> unresolved = ref GetUnresolvedParameters(parameterProcessor);
        unresolved.Remove(parameter);

        if (unresolved.Count == 0)
        {
            GetAllParametersResolvedCts(parameterProcessor)?.Cancel();
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_WaitForValueTcs")]
    static extern TaskCompletionSource<string>? GetWaitForValueTcs(ParameterResource parameter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_unresolvedParameters")]
    static extern ref List<ParameterResource> GetUnresolvedParameters(ParameterProcessor processor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_allParametersResolvedCts")]
    static extern ref CancellationTokenSource? GetAllParametersResolvedCts(ParameterProcessor processor);
}
#pragma warning restore ASPIREINTERACTION001
