// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.GlitchTip;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sentry.AspNetCore;
using Sentry.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>Adds ASP.NET Core request support to the shared GlitchTip client.</summary>
public static class GlitchTipAspNetCoreExtensions
{
    /// <summary>
    /// Adds the common GlitchTip client and ASP.NET Core request middleware. The web and common registrations
    /// share one options object, logging provider, and SDK lifecycle, regardless of registration order.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="connectionName">The reporting connection supplied by the AppHost.</param>
    /// <param name="configureSettings">Optional client configuration.</param>
    /// <param name="configureSentry">Optional ASP.NET Core SDK customization.</param>
    /// <returns>The web application builder.</returns>
    public static WebApplicationBuilder AddGlitchTipAspNetCore(
        this WebApplicationBuilder builder,
        string connectionName,
        Action<GlitchTipClientSettings>? configureSettings = null,
        Action<SentryAspNetCoreOptions>? configureSentry = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddGlitchTipClient(connectionName, configureSettings);
        GlitchTipRegistration registration = GlitchTipClientExtensions.GetRegistration(builder.Services)!;
        if (configureSentry is not null)
        {
            registration.ConfigureSentry.Add(options => configureSentry((SentryAspNetCoreOptions)options));
        }

        if (registration.HasAspNetCore)
        {
            return builder;
        }

        registration.HasAspNetCore = true;
        // Upgrade only the registrations owned by the common module to the native web integration.
        // UseSentry registers its internal request middleware and its matching logger provider.
        foreach (ServiceDescriptor descriptor in registration.LoggingServices)
        {
            builder.Services.Remove(descriptor);
        }
        registration.LoggingServices.Clear();
        builder.WebHost.UseSentry();
        builder.Services.PostConfigure<SentryAspNetCoreOptions>(options =>
        {
            options.AutoRegisterTracing = false;
            options.AdjustStandardEnvironmentNameCasing = false;
            options.FlushOnCompletedRequest = false;
            registration.Apply(options);
        });
        builder.Services.Replace(ServiceDescriptor.Singleton<IOptions<SentryLoggingOptions>>(static provider =>
            new OptionsWrapper<SentryLoggingOptions>(provider.GetRequiredService<IOptions<SentryAspNetCoreOptions>>().Value)));
        return builder;
    }
}
