// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentry;
using Sentry.Extensibility;
using Sentry.Extensions.Logging;
using Sentry.OpenTelemetry;

namespace CommunityToolkit.Aspire.GlitchTip;

internal sealed class GlitchTipRegistration(string connectionName, IConfiguration configuration, GlitchTipClientSettings settings)
{
    internal string ConnectionName { get; } = connectionName;
    internal GlitchTipClientSettings Settings { get; } = settings;
    internal List<Action<SentryLoggingOptions>> ConfigureSentry { get; } = [];
    internal bool HasAspNetCore { get; set; }
    internal List<ServiceDescriptor> LoggingServices { get; } = [];

    internal void Apply(SentryLoggingOptions options)
    {
        Validate();
        string dsn = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"The GlitchTip connection string '{ConnectionName}' is required. Reference the GlitchTip resource from the AppHost.");
        if (string.IsNullOrWhiteSpace(dsn))
        {
            throw new InvalidOperationException($"The GlitchTip connection string '{ConnectionName}' must not be empty.");
        }

        options.SendDefaultPii = false;
        options.IsEnvironmentUser = false;
        options.AutoSessionTracking = false;
        options.EnableMetrics = false;
        options.SetBeforeSendLog(log =>
        {
            log.SetAttribute("service.name", Settings.ServiceName!);
            return log;
        });
        options.TracesSampleRate = Settings.EnableTracing ? Settings.TracesSampleRate : 0;

        foreach (Action<SentryLoggingOptions> configure in ConfigureSentry)
        {
            configure(options);
        }

        // Resource identity and signal switches are the integration contract; native callbacks customize SDK behavior.
        options.Dsn = dsn;
        options.Environment = Settings.Environment;
        options.Release = Settings.Release;
        options.DefaultTags["service.name"] = Settings.ServiceName!;
        options.InitializeSdk = true;
        options.ShutdownTimeout = Settings.ShutdownTimeout;
        options.EnableLogs = Settings.EnableLogs;
        if (!Settings.EnableErrors)
        {
            options.MinimumEventLevel = LogLevel.None;
            options.AddEventProcessor(new SuppressErrorsProcessor());
        }

        if (Settings.EnableTracing)
        {
            // The envelope bridge creates Sentry transactions from Activity. Disabling Sentry tracing
            // also discards those transactions in SDK 6.6; Instrumenter still selects OpenTelemetry.
            options.UseOpenTelemetry();
        }
        else
        {
            options.TracesSampler = null;
            options.TracesSampleRate = 0;
        }
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Settings.Environment) || string.IsNullOrWhiteSpace(Settings.Release) || string.IsNullOrWhiteSpace(Settings.ServiceName))
        {
            throw new InvalidOperationException("Aspire:GlitchTip must contain Environment, Release, and ServiceName from the AppHost. Individual service hosting environments are not used as a fallback.");
        }

        if (!double.IsFinite(Settings.TracesSampleRate) || Settings.TracesSampleRate is < 0 or > 1)
        {
            throw new InvalidOperationException("Aspire:GlitchTip:TracesSampleRate must be between zero and one.");
        }

        if (Settings.ShutdownTimeout <= TimeSpan.Zero || Settings.ShutdownTimeout > TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException("Aspire:GlitchTip:ShutdownTimeout must be greater than zero and at most 30 seconds.");
        }
    }

    private sealed class SuppressErrorsProcessor : ISentryEventProcessor
    {
        public SentryEvent? Process(SentryEvent @event)
        {
            return null;
        }
    }
}
