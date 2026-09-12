// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Extensions.Logging;
using Sentry.Integrations;
using Sentry.Protocol.Envelopes;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CommunityToolkit.Aspire.GlitchTip.Tests;

public class GlitchTipClientTests
{
    [Fact]
    public void BindsAppHostIdentityAndSafeDefaults()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Environment.EnvironmentName = "ServiceEnvironmentMustNotLeak";
        builder.AddGlitchTipClient("glitchtip");
        using IHost host = builder.Build();
        SentryLoggingOptions options = host.Services.GetRequiredService<IOptions<SentryLoggingOptions>>().Value;

        Assert.Equal("http://public@127.0.0.1:1/1", options.Dsn);
        Assert.Equal("production", options.Environment);
        Assert.Equal("release-123", options.Release);
        Assert.Equal("worker", options.DefaultTags["service.name"]);
        Assert.False(options.SendDefaultPii);
        Assert.False(options.IsEnvironmentUser);
        Assert.False(options.AutoSessionTracking);
        Assert.False(options.EnableLogs);
        Assert.False(options.EnableMetrics);
        Assert.Equal(0, options.TracesSampleRate);
        Assert.Equal(TimeSpan.FromSeconds(2), options.ShutdownTimeout);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CommonAndWebUseOneSdkAndPreserveCallbacks(bool commonFirst)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        Configure(builder.Configuration);
        RecordingTransport transport = new();
        CountingIntegration integration = new();
        int commonCalls = 0;
        int webCalls = 0;

        void AddCommon()
        {
            builder.AddGlitchTipClient("glitchtip", configureSentry: options =>
            {
                commonCalls++;
                options.Transport = transport;
                options.AddIntegration(integration);
                options.DefaultTags["application-policy"] = "preserved";
            });
        }
        void AddWeb()
        {
            builder.AddGlitchTipAspNetCore("glitchtip", configureSentry: options =>
            {
                webCalls++;
                options.MaxRequestBodySize = RequestSize.Small;
            });
        }
        if (commonFirst)
        {
            AddCommon();
            AddWeb();
        }
        else
        {
            AddWeb();
            AddCommon();
        }
        builder.AddGlitchTipClient("glitchtip");
        builder.AddGlitchTipAspNetCore("glitchtip");
        await using WebApplication app = builder.Build();
        SentryLoggingOptions common = app.Services.GetRequiredService<IOptions<SentryLoggingOptions>>().Value;
        SentryAspNetCoreOptions web = app.Services.GetRequiredService<IOptions<SentryAspNetCoreOptions>>().Value;

        Assert.Same(common, web);
        Assert.Equal(1, commonCalls);
        Assert.Equal(1, webCalls);
        Assert.Equal(1, integration.Initializations);
        Assert.Equal(RequestSize.Small, web.MaxRequestBodySize);
        Assert.Equal("preserved", web.DefaultTags["application-policy"]);
        Assert.False(web.AutoRegisterTracing);
        Assert.False(web.AdjustStandardEnvironmentNameCasing);
        Assert.False(web.FlushOnCompletedRequest);
        Assert.Single(app.Services.GetServices<ILoggerProvider>(), static provider => provider.GetType().Name == "SentryAspNetCoreLoggerProvider");
        Assert.Single(app.Services.GetServices<IStartupFilter>().OfType<SentryStartupFilter>());
    }

    [Theory]
    [InlineData("Aspire:GlitchTip:Environment", "")]
    [InlineData("Aspire:GlitchTip:Release", "")]
    [InlineData("Aspire:GlitchTip:ServiceName", "")]
    [InlineData("Aspire:GlitchTip:TracesSampleRate", "1.1")]
    [InlineData("Aspire:GlitchTip:TracesSampleRate", "NaN")]
    [InlineData("Aspire:GlitchTip:ShutdownTimeout", "00:01:00")]
    [InlineData("ConnectionStrings:glitchtip", "")]
    public void InvalidConfigurationDoesNotSilentlyDisableReporting(string key, string value)
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Configuration[key] = value;
        builder.AddGlitchTipClient("glitchtip");
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void DifferentProjectConnectionsAreRejected()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGlitchTipClient("glitchtip");
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.AddGlitchTipClient("other-project"));
        Assert.Contains("one GlitchTip project", error.Message);
    }

    [Fact]
    public async Task ErrorsAndStructuredLogsAreIndependent()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.Logging.ClearProviders();
        RecordingTransport transport = new();
        builder.AddGlitchTipClient("glitchtip", settings =>
        {
            settings.EnableErrors = false;
            settings.EnableLogs = true;
        }, options => options.Transport = transport);
        using IHost host = builder.Build();
        await host.StartAsync();

        SentrySdk.CaptureException(new InvalidOperationException("suppressed-direct-error"));
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("client-test");
        logger.LogError("structured-error-log");
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));

        Assert.DoesNotContain(transport.Envelopes, static json => json.Contains("\"type\":\"event\"", StringComparison.Ordinal));
        Assert.Contains(transport.Envelopes, static json => json.Contains("structured-error-log", StringComparison.Ordinal));
        Assert.Contains(transport.Envelopes, static json => json.Contains("service.name", StringComparison.Ordinal));
        await host.StopAsync();
    }

    [Fact]
    public async Task WebMiddlewareCapturesRequestFailuresWithSharedClient()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        Configure(builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        RecordingTransport transport = new();
        builder.AddGlitchTipAspNetCore("glitchtip", configureSentry: options => options.Transport = transport);
        await using WebApplication app = builder.Build();
        app.MapGet("/failure", static () => ThrowRequestFailure());
        await app.StartAsync();
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(app.Urls.Single() + "/failure");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(transport.Envelopes, static json => json.Contains("request-failure", StringComparison.Ordinal)
            && json.Contains("/failure", StringComparison.Ordinal));
        await app.StopAsync();
    }

    [Fact]
    public async Task ExistingOpenTelemetryExporterAndPropagationRemainAvailable()
    {
        TextMapPropagator original = Propagators.DefaultTextMapPropagator;
        try
        {
            List<Activity> exported = [];
            RecordingTransport transport = new();
            HostApplicationBuilder builder = CreateBuilder();
            builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
                .AddSource("application-source")
                .SetSampler(new AlwaysOnSampler())
                .AddInMemoryExporter(exported));
            builder.AddGlitchTipClient("glitchtip", settings => settings.EnableTracing = true, options => options.Transport = transport);
            using IHost host = builder.Build();
            await host.StartAsync();
            using ActivitySource source = new("application-source");
            using (Activity? activity = source.StartActivity("retained-export"))
            {
                Assert.NotNull(activity);
            }
            host.Services.GetRequiredService<TracerProvider>().ForceFlush();
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(exported, static activity => activity.DisplayName == "retained-export");
            Assert.Contains(transport.Envelopes, static json => json.Contains("retained-export", StringComparison.Ordinal));
            Assert.All(original.Fields ?? new HashSet<string>(), field => Assert.Contains(field, Propagators.DefaultTextMapPropagator.Fields!));
            Assert.Contains("sentry-trace", Propagators.DefaultTextMapPropagator.Fields!);
            await host.StopAsync();
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(original);
        }
    }

    [Fact]
    public async Task ExistingOpenTelemetrySamplerIsNotReplaced()
    {
        TextMapPropagator original = Propagators.DefaultTextMapPropagator;
        try
        {
            HostApplicationBuilder builder = CreateBuilder();
            builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource("not-sampled").SetSampler(new AlwaysOffSampler()));
            builder.AddGlitchTipClient("glitchtip", settings => settings.EnableTracing = true);
            using IHost host = builder.Build();
            await host.StartAsync();
            using ActivitySource source = new("not-sampled");
            using Activity? activity = source.StartActivity("not-exported");
            Assert.False(activity?.Recorded ?? false);
            await host.StopAsync();
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(original);
        }
    }

    [Fact]
    public async Task DeliveryFailureDoesNotFailHostOrCaptureAndFlushIsBounded()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGlitchTipClient("glitchtip", settings => settings.ShutdownTimeout = TimeSpan.FromMilliseconds(100),
            options => options.Transport = new FailingTransport());
        using IHost host = builder.Build();
        await host.StartAsync();
        SentrySdk.CaptureException(new InvalidOperationException("cannot-deliver"));
        await SentrySdk.FlushAsync(TimeSpan.FromMilliseconds(100));
        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static string ThrowRequestFailure()
    {
        throw new InvalidOperationException("request-failure");
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        Configure(builder.Configuration);
        return builder;
    }

    private static void Configure(ConfigurationManager configuration)
    {
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:glitchtip"] = "http://public@127.0.0.1:1/1",
            ["Aspire:GlitchTip:Environment"] = "production",
            ["Aspire:GlitchTip:Release"] = "release-123",
            ["Aspire:GlitchTip:ServiceName"] = "worker"
        });
    }

    private sealed class CountingIntegration : ISdkIntegration
    {
        internal int Initializations { get; private set; }
        public void Register(IHub hub, SentryOptions options)
        {
            Initializations++;
        }
    }

    private sealed class RecordingTransport : ITransport
    {
        internal ConcurrentQueue<string> Envelopes { get; } = new();
        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            await using MemoryStream stream = new();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            Envelopes.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
        }
    }

    private sealed class FailingTransport : ITransport
    {
        public Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            return Task.FromException(new HttpRequestException("The reporting server is unavailable."));
        }
    }
}
