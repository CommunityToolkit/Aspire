// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.GlitchTip;

var builder = DistributedApplication.CreateBuilder(args);
builder.AddDockerComposeEnvironment("compose");
var project = builder.AddParameter("glitchtip-project", "glitchtip-demo");
var release = builder.AddParameter("release", "local");
var glitchtip = builder.AddGlitchTip("glitchtip", project, release);
var api = builder.AddProject<Projects.GlitchTip_Api>("api")
    .WithHttpEndpoint(name: "http")
    .WithGlitchTipHealthCheck()
    .WithGlitchTipTelemetry(new GlitchTipTelemetryOptions { EnableLogs = true, EnableTracing = true })
    .WithReference(glitchtip);
if (builder.ExecutionContext.IsPublishMode)
{
    var healthUrl = builder.AddParameter("api-health-url");
    api.WithGlitchTipMonitorUrl(ReferenceExpression.Create($"{healthUrl}"));
}
await builder.Build().RunAsync();
