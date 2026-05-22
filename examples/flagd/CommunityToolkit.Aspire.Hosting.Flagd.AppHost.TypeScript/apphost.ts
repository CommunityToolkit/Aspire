import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

const flagd = await builder.addFlagd("flagd", {
    port: 18013,
    ofrepPort: 18016,
});

const flagdDefault = await builder.addFlagd("flagd-default");

await flagd.withBindFileSync("./flags");
await flagdDefault.withBindFileSync("./flags", {
    filename: "custom-flagd.json",
});

const _primaryEndpoint = await flagd.primaryEndpoint();
const _host = await flagd.host();
const _port = await flagd.port();
const _healthCheckEndpoint = await flagd.getEndpoint("health");
const _ofrepEndpoint = await flagd.getEndpoint("ofrep");
const _uriExpression = await flagd.uriExpression();
const _connectionStringExpression = await flagd.connectionStringExpression();

const _defaultPrimaryEndpoint = await flagdDefault.primaryEndpoint();
const _defaultHost = await flagdDefault.host();
const _defaultPort = await flagdDefault.port();
const _defaultHealthCheckEndpoint = await flagdDefault.getEndpoint("health");
const _defaultOfrepEndpoint = await flagdDefault.getEndpoint("ofrep");
const _defaultUriExpression = await flagdDefault.uriExpression();
const _defaultConnectionStringExpression =
    await flagdDefault.connectionStringExpression();

await builder.build().run();
