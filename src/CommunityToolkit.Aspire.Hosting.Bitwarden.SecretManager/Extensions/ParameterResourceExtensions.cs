using System.Runtime.CompilerServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;

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
            string? value = parameter.GetValueAsync(CancellationToken.None).GetAwaiter().GetResult();
            return !string.IsNullOrWhiteSpace(value);
        }

#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        public async ValueTask PromptAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(services);

            ParameterProcessor parameterProcessor = services.GetRequiredService<ParameterProcessor>();
            await parameterProcessor.SetParameterAsync(parameter, cancellationToken).ConfigureAwait(false);
        }
#pragma warning restore ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_WaitForValueTcs")]
    static extern TaskCompletionSource<string>? GetWaitForValueTcs(ParameterResource parameter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_WaitForValueTcs")]
    static extern void SetWaitForValueTcs(ParameterResource parameter, TaskCompletionSource<string>? value);
}
