// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.GlitchTip;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using Sentry.Extensions.Logging;
using Sentry.OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>Registers the GlitchTip client for .NET applications.</summary>
public static class GlitchTipClientExtensions
{
    /// <summary>
    /// Adds error reporting, optional structured logs, and an optional Sentry bridge to the existing OpenTelemetry pipeline.
    /// Calling this method again for the same connection adds configuration without initializing another SDK.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="connectionName">The connection string containing the reporting DSN supplied by Aspire.</param>
    /// <param name="configureSettings">Optional client configuration.</param>
    /// <param name="configureSentry">Optional native SDK customization, including application-owned event processors.</param>
    /// <returns>The application builder.</returns>
    public static IHostApplicationBuilder AddGlitchTipClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<GlitchTipClientSettings>? configureSettings = null,
        Action<SentryLoggingOptions>? configureSentry = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        GlitchTipRegistration? registration = GetRegistration(builder.Services);
        if (registration is not null)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(registration.ConnectionName, connectionName))
            {
                throw new InvalidOperationException("One Aspire stack uses one GlitchTip project. Register only one GlitchTip connection per application.");
            }

            configureSettings?.Invoke(registration.Settings);
            if (configureSentry is not null)
            {
                registration.ConfigureSentry.Add(configureSentry);
            }

            return builder;
        }

        GlitchTipClientSettings settings = new();
        builder.Configuration.GetSection(GlitchTipClientSettings.SectionName).Bind(settings);
        configureSettings?.Invoke(settings);
        registration = new(connectionName, builder.Configuration, settings);
        if (configureSentry is not null)
        {
            registration.ConfigureSentry.Add(configureSentry);
        }

        builder.Services.AddSingleton(registration);
        builder.Services.AddHostedService<GlitchTipFlushService>();
        HashSet<ServiceDescriptor> existingServices = [.. builder.Services];
        builder.Logging.AddSentry();
        registration.LoggingServices.AddRange(builder.Services.Where(descriptor => !existingServices.Contains(descriptor)));
        builder.Services.PostConfigure<SentryLoggingOptions>(registration.Apply);
        builder.Services.AddOpenTelemetry().WithTracing(tracing =>
        {
            if (registration.Settings.EnableTracing)
            {
                // AddSentry changes the global propagator. Preserve the application's existing propagation formats.
                TextMapPropagator existing = Propagators.DefaultTextMapPropagator;
                tracing.AddSentry(new CompositeTextMapPropagator([existing, new SentryPropagator()]));
            }
        });

        return builder;
    }

    internal static GlitchTipRegistration? GetRegistration(IServiceCollection services)
    {
        return services.SingleOrDefault(static descriptor => descriptor.ServiceType == typeof(GlitchTipRegistration))?.ImplementationInstance as GlitchTipRegistration;
    }
}
