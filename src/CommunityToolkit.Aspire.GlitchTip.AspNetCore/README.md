# CommunityToolkit.Aspire.GlitchTip.AspNetCore

Adds ASP.NET Core request exception capture to the [common GlitchTip client](../CommunityToolkit.Aspire.GlitchTip/README.md).

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddGlitchTipAspNetCore("glitchtip");
var app = builder.Build();
app.MapGet("/", () => "Hello");
app.Run();
```

The AppHost must reference the stack's GlitchTip resource from this web project. It supplies the reporting DSN, environment, effective release, and stable service name.

`AddGlitchTipAspNetCore` adds the common client automatically. It is also safe to call `AddGlitchTipClient` in shared service defaults before or after this registration. Both methods share one SDK options object and lifecycle. Common and web native configuration callbacks run on the same `SentryAspNetCoreOptions` object in registration order.

```csharp
builder.AddGlitchTipAspNetCore("glitchtip", configureSentry: options =>
{
    options.AddEventProcessor(new ApplicationEventProcessor());
});
```

Request middleware is installed by the native Sentry web integration; no manual `app.UseSentry()` call is needed. Personal-data collection and request-completion flushing start disabled. The AppHost environment retains its exact casing and is not inferred from the web hosting environment. The integration does not install a native Sentry request tracing pipeline: tracing uses the application's existing OpenTelemetry pipeline when enabled.

Signal controls, structured-log filtering, shutdown bounds, and application-owned processors follow the common client contract. Do not separately initialize the SDK or register the native Sentry hosting/logging integration.
