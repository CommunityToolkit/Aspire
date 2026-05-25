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

const _primaryEndpoint = await goff.primaryEndpoint();
const _host = await goff.host();
const _port = await goff.port();
const _uri = await goff.uriExpression();
const _connectionString = await goff.connectionStringExpression();

const _primaryEndpoint2 = await goff2.primaryEndpoint();
const _host2 = await goff2.host();
const _port2 = await goff2.port();
const _uri2 = await goff2.uriExpression();
const _connectionString2 = await goff2.connectionStringExpression();

await builder.build().run();
