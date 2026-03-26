import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addK6 — minimal overload (defaults)
const defaultK6 = await builder.addK6("k6-default");

// addK6 — exercise optional parameters
const browserK6 = await builder.addK6("k6-browser", {
    enableBrowserExtensions: true,
    port: 6566
});

// withBindMount — mount the local scripts directory into each container
await defaultK6.withBindMount("./scripts", "/scripts", { isReadOnly: true });
await browserK6.withBindMount("./scripts", "/scripts", { isReadOnly: true });

// withScript — default optional parameters
await defaultK6.withScript("/scripts/main.js");

// withK6OtlpEnvironment — no-arg fluent method
await defaultK6.withK6OtlpEnvironment();

// withScript — explicit optional parameters
await browserK6.withScript("/scripts/main.js", {
    virtualUsers: 5,
    duration: "45s"
});

// withK6OtlpEnvironment — chain on second resource too
await browserK6.withK6OtlpEnvironment();

// ---- Property access on K6Resource (ExposeProperties = true) ----
const defaultK6Resource = await defaultK6;
const _defaultPrimaryEndpoint = await defaultK6Resource.primaryEndpoint.get();

const browserK6Resource = await browserK6;
const _browserPrimaryEndpoint = await browserK6Resource.primaryEndpoint.get();

await builder.build().run();
