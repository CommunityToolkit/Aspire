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
            using var cts = new CancellationTokenSource();
            var task = parameter.GetValueAsync(cts.Token).AsTask();
            try
            {
                return task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(task.Result);
            }
            finally
            {
                cts.Cancel();
                task.Dispose();
            }
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
}
