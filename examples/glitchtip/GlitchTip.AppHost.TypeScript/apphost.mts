// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const project = await builder.addParameter("glitchtip-project", { value: "glitchtip-typescript" });
const release = await builder.addParameter("release", { value: "local" });
const glitchtip = await builder.addGlitchTip("glitchtip", project, release);
await glitchtip.withProjectDisplayName(await builder.addParameter("display-name", { value: "TypeScript example" }));
await glitchtip.withMonitorDefaults({ intervalSeconds: 60, timeoutSeconds: 20 });
// Enable explicitly when registered artifact paths are prepared before local startup.
await glitchtip.withLocalArtifactUploads({ enabled: false });

const web = await builder.addContainer("web", "nginx:1.29-alpine");
await web.withHttpEndpoint({ targetPort: 80 });
await web.withGlitchTipHealthCheck({ path: "/" });
await web.withGlitchTipMonitor({ path: "/", options: { intervalSeconds: 30, timeoutSeconds: 10 } });
await web.withGlitchTipTelemetry({ enableErrors: true, enableLogs: false, enableTracing: false });
await web.withParameterGlitchTipRelease(release);
await web.withGlitchTipReference(glitchtip);
// The nginx example exposes a monitored endpoint. Sending telemetry still requires a Sentry SDK in the workload.
await builder.build().run();
