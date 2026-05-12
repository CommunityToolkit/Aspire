import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const flagd = await builder.addFlagd('flagd', {
    port: 18013,
    ofrepPort: 18016
});

const flagdDefault = await builder.addFlagd('flagd-default');

await flagd.withBindFileSync('./flags');
await flagdDefault.withBindFileSync('./flags', { filename: 'custom-flagd.json' });

const flagdResource = await flagd;
const _primaryEndpoint = await flagdResource.getEndpoint("http");
const _host = await _primaryEndpoint.host.get();
const _port = await _primaryEndpoint.port.get();
const _healthCheckEndpoint = await flagdResource.getEndpoint("health");
const _ofrepEndpoint = await flagdResource.getEndpoint("ofrep");
const _uriExpression = await _primaryEndpoint.url.get();
const _connectionStringExpression = await flagdResource.connectionStringExpression.get();

const flagdDefaultResource = await flagdDefault;
const _defaultPrimaryEndpoint = await flagdDefaultResource.getEndpoint("http");
const _defaultHost = await _defaultPrimaryEndpoint.host.get();
const _defaultPort = await _defaultPrimaryEndpoint.port.get();
const _defaultHealthCheckEndpoint = await flagdDefaultResource.getEndpoint("health");
const _defaultOfrepEndpoint = await flagdDefaultResource.getEndpoint("ofrep");
const _defaultUriExpression = await _defaultPrimaryEndpoint.url.get();
const _defaultConnectionStringExpression = await flagdDefaultResource.connectionStringExpression.get();

await builder.build().run();
