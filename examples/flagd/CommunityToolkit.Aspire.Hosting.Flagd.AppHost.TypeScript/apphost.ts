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
const _primaryEndpoint = await flagdResource.primaryEndpoint.get();
const _host = await flagdResource.host.get();
const _port = await flagdResource.port.get();
const _healthCheckEndpoint = await flagdResource.healthCheckEndpoint.get();
const _ofrepEndpoint = await flagdResource.ofrepEndpoint.get();
const _uriExpression = await flagdResource.uriExpression.get();
const _connectionStringExpression = await flagdResource.connectionStringExpression.get();

const flagdDefaultResource = await flagdDefault;
const _defaultPrimaryEndpoint = await flagdDefaultResource.primaryEndpoint.get();
const _defaultHost = await flagdDefaultResource.host.get();
const _defaultPort = await flagdDefaultResource.port.get();
const _defaultHealthCheckEndpoint = await flagdDefaultResource.healthCheckEndpoint.get();
const _defaultOfrepEndpoint = await flagdDefaultResource.ofrepEndpoint.get();
const _defaultUriExpression = await flagdDefaultResource.uriExpression.get();
const _defaultConnectionStringExpression = await flagdDefaultResource.connectionStringExpression.get();

await builder.build().run();
