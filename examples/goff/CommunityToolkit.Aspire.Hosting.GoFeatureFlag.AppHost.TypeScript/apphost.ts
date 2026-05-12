import { createBuilder, GoFeatureFlagLogLevel } from './.modules/aspire.js';

const builder = await createBuilder();

const goff = await builder.addGoFeatureFlag("goff", {
    pathToConfigFile: "/goff/goff-proxy.yaml",
    port: 11031
});

await goff.withDataVolume({ name: "goff-data" });
await goff.withGoffBindMount("./goff");
await goff.withLogLevel(GoFeatureFlagLogLevel.Debug);

const goff2 = await builder.addGoFeatureFlag("goff2");
await goff2.withGoffBindMount("./goff");

const goffResource = await goff;
const _primaryEndpoint = await goffResource.getEndpoint("http");
const _host = await _primaryEndpoint.host.get();
const _port = await _primaryEndpoint.port.get();
const _uri = await _primaryEndpoint.url.get();
const _connectionString = await goffResource.connectionStringExpression.get();

const goff2Resource = await goff2;
const _primaryEndpoint2 = await goff2Resource.getEndpoint("http");
const _host2 = await _primaryEndpoint2.host.get();
const _port2 = await _primaryEndpoint2.port.get();
const _uri2 = await _primaryEndpoint2.url.get();
const _connectionString2 = await goff2Resource.connectionStringExpression.get();

await builder.build().run();
