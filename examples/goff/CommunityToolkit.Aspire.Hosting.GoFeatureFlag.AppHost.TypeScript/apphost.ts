import { createBuilder, GoFeatureFlagLogLevel } from "./.modules/aspire.js";

const builder = await createBuilder();

const goff = await builder.addGoFeatureFlag("goff", {
    pathToConfigFile: "/goff/goff-proxy.yaml",
    port: 11031,
});

await goff.withDataVolume({ name: "goff-data" });
await goff.withGoffBindMount("./goff");
await goff.withLogLevel(GoFeatureFlagLogLevel.Debug);

const goff2 = await builder.addGoFeatureFlag("goff2");
await goff2.withGoffBindMount("./goff");

const _primaryEndpoint = await goff.getEndpoint("http");
const _host = await _primaryEndpoint.host();
const _port = await _primaryEndpoint.port();
const _uri = await _primaryEndpoint.url();
const _connectionString = await goff.connectionStringExpression();

const _primaryEndpoint2 = await goff2.getEndpoint("http");
const _host2 = await _primaryEndpoint2.host();
const _port2 = await _primaryEndpoint2.port();
const _uri2 = await _primaryEndpoint2.url();
const _connectionString2 = await goff2.connectionStringExpression();

await builder.build().run();
