// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentry;
using Sentry.Extensions.Logging;

namespace CommunityToolkit.Aspire.GlitchTip;

internal sealed class GlitchTipFlushService(IHub hub, IOptions<SentryLoggingOptions> options, ILogger<GlitchTipFlushService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            TimeSpan timeout = options.Value.ShutdownTimeout;
            await hub.FlushAsync(timeout).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Telemetry transport must never turn application shutdown into a failure.
            logger.LogWarning("GlitchTip shutdown flush did not complete ({FailureType}).", exception.GetType().Name);
        }
    }
}
