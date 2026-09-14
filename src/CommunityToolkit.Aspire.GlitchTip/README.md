# CommunityToolkit.Aspire.GlitchTip

Registers the Sentry .NET SDK for a GlitchTip project supplied by an Aspire AppHost. Worker services use this package; web applications use [CommunityToolkit.Aspire.GlitchTip.AspNetCore](../CommunityToolkit.Aspire.GlitchTip.AspNetCore/README.md).

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddGlitchTipClient("glitchtip");
```

The hosting integration supplies `ConnectionStrings:glitchtip` and `Aspire:GlitchTip:Environment`, `Release`, and `ServiceName`. Environment is the AppHost environment, including the `aspire deploy --environment` selection. Individual application hosting environments do not override it. The release must match the service's registered deployment artifacts. A missing DSN or missing identity configuration is an error; the client does not query the management API, provision projects, or cache a DSN.

One stack uses one GlitchTip project. All services reference that project. Repeated registration with the same connection adds configuration without another SDK initialization; registering a second connection fails. Use these registration methods as the SDK lifecycle owner, without separately calling `SentrySdk.Init`, `AddSentry`, or `UseSentry`.

## Signals and customization

| Setting under `Aspire:GlitchTip` | Default | Behavior |
| --- | --- | --- |
| `EnableErrors` | `true` | Capture error events, including SDK exception capture and error-level application logs. |
| `EnableLogs` | `false` | Send structured SDK log records independently of error events. |
| `EnableTracing` | `false` | Add the Sentry envelope bridge to the existing OpenTelemetry trace pipeline. |
| `TracesSampleRate` | `1` | Sentry transaction sampling fraction when tracing is enabled. Valid range: zero to one. |
| `ShutdownTimeout` | `00:00:02` | Maximum shutdown flush duration. Must be positive and no longer than 30 seconds. |

```csharp
builder.AddGlitchTipClient("glitchtip",
    configureSettings: settings =>
    {
        settings.EnableLogs = true;
        settings.EnableTracing = true;
        settings.TracesSampleRate = 0.1;
    },
    configureSentry: options =>
    {
        options.MinimumEventLevel = LogLevel.Error;
        options.AddEventProcessor(new ApplicationEventProcessor());
    });
```

Personal-data collection, environment-user detection, automatic session tracking, and SDK metrics start disabled. Native SDK options remain available for application policy, processors, and filters. Resource identity, signal enable switches, SDK initialization, and bounded shutdown remain owned by the integration. Native SDK settings do not replace the reporting connection, AppHost environment, effective release, or service identity.

Events and transactions receive a `service.name` tag. Structured logs receive a `service.name` attribute through a default `SetBeforeSendLog` callback. Sentry exposes a single replaceable callback: applications that replace `SetBeforeSendLog` for filtering must retain that attribute in their callback. Microsoft logging severity/category rules apply to structured logs. `MinimumEventLevel` and `AddLogEntryFilter` control error events and breadcrumbs separately; they do not replace structured-log filtering. When errors and structured logs are both enabled, an error-level log can intentionally produce both an error event and a structured log record.

## OpenTelemetry interoperability

The integration adds a Sentry processor without replacing exporters or the application's sampler. The application continues to choose its activity sources and instrumentation. Existing propagation formats are retained alongside Sentry propagation. Tracing disabled leaves no Sentry trace processor on the pipeline.

Sentry transaction sampling is an additional decision after OpenTelemetry sampling. A span dropped by OpenTelemetry cannot be recovered by a larger Sentry sample rate. The SDK's custom `TracesSampler` can be set through the native options callback when needed.

This package uses the tested Sentry 6.6 envelope bridge. Its `UseOpenTelemetry()` configuration selects the OpenTelemetry instrumenter. The `disableSentryTracing: true` variant also discards transactions produced by that bridge in this SDK version, so the integration does not use it. Do not add a parallel native Sentry tracing pipeline.

## Runtime behavior

There is no GlitchTip availability readiness check and no network request during registration. SDK delivery runs in the background; an unavailable reporting server does not fail application startup, request handling, or readiness. Host shutdown attempts a bounded flush; failure remains diagnostic. SDK diagnostic logging can be enabled through native options for troubleshooting, but it can print the reporting DSN and should not be enabled in routine operation.

Management/provisioning failures belong to the hosting/deployment integration and block startup or deployment. These client settings do not add an automatic fallback or a deployment disable switch.

## Other runtimes

The hosting contract supplies the DSN plus AppHost environment, effective release, and stable service name. Non-.NET applications configure a compatible SDK themselves; this package does not initialize their SDK, select instrumentation, or map language-specific environment variables automatically.
