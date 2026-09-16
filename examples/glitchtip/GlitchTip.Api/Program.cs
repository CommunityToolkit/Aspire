// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.AddGlitchTipAspNetCore("glitchtip");
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());
var app = builder.Build();
app.MapGet("/health", () => Results.Ok("healthy"));
app.MapGet("/log", (ILogger<Program> logger) => { logger.LogWarning("GlitchTip example log"); return Results.Ok("logged"); });
app.MapGet("/", () => "GlitchTip example. GET /error reports a controlled example failure.");
app.MapGet("/error", IResult () => throw new InvalidOperationException("GlitchTip example failure"));
await app.RunAsync();
