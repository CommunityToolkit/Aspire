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

const _primaryEndpoint = await flagd.getEndpoint("http");
const _host = await _primaryEndpoint.host();
const _port = await _primaryEndpoint.port();
const _healthCheckEndpoint = await flagd.getEndpoint("health");
const _ofrepEndpoint = await flagd.getEndpoint("ofrep");
const _uriExpression = await _primaryEndpoint.url();
const _connectionStringExpression = await flagd.connectionStringExpression();

const _defaultPrimaryEndpoint = await flagdDefault.getEndpoint("http");
const _defaultHost = await _defaultPrimaryEndpoint.host();
const _defaultPort = await _defaultPrimaryEndpoint.port();
const _defaultHealthCheckEndpoint = await flagdDefault.getEndpoint("health");
const _defaultOfrepEndpoint = await flagdDefault.getEndpoint("ofrep");
const _defaultUriExpression = await _defaultPrimaryEndpoint.url();
const _defaultConnectionStringExpression =
    await flagdDefault.connectionStringExpression();

await builder.build().run();
