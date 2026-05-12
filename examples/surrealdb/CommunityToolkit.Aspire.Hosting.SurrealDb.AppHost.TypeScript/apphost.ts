import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

let primary = await builder.addSurrealServer('primary', {
    port: 18000,
    path: 'memory',
    strictMode: true,
});

primary = await primary.withDataVolume({ name: 'surreal-primary-data' });
primary = await primary.withLogLevel('Information');
primary = await primary.withSurrealist({ containerName: 'surrealist-primary' });
primary = await primary.withSurrealDbOtlpExporter();

let appNamespace = await primary.addNamespace('appns', { namespaceName: 'polyglotNs' });
appNamespace = await appNamespace.withCreationScript('DEFINE NAMESPACE IF NOT EXISTS `polyglotNs`;');

let appDatabase = await appNamespace.addDatabase('appdb', { databaseName: 'polyglotDb' });
appDatabase = await appDatabase.withCreationScript('DEFINE DATABASE IF NOT EXISTS `polyglotDb`;');

let mounted = await builder.addSurrealServer('mounted', {
    port: 18001,
    path: 'memory',
});
mounted = await mounted.withDataBindMount('./data');
mounted = await mounted.withInitFiles('./seed.surql');

const primaryResource = await primary;
const _primaryEndpoint = await primaryResource.getEndpoint('tcp');
const _primaryHost = await _primaryEndpoint.host.get();
const _primaryPort = await _primaryEndpoint.port.get();
const _primaryPasswordParameter = await primaryResource.passwordParameter.get();
const _primaryUri = await _primaryEndpoint.url.get();
const _primaryConnectionString = await primaryResource.connectionStringExpression.get();

const namespaceResource = await appNamespace;
const _namespaceParent = await namespaceResource.parent.get();
const _namespaceConnectionString = await namespaceResource.connectionStringExpression.get();
const _namespaceName = await namespaceResource.namespaceName.get();
const _namespaceParentName = await _namespaceParent.name.get();

if (false) {
    const _primaryNamespace = await primaryResource.namespaces.get('appns');
    const _namespaceDatabase = await namespaceResource.databases.get('appdb');
}

const databaseResource = await appDatabase;
const _databaseParent = await databaseResource.parent.get();
const _databaseConnectionString = await databaseResource.connectionStringExpression.get();
const _databaseName = await databaseResource.databaseName.get();
const _databaseParentName = await _databaseParent.name.get();
const _databaseServerName = await (await _databaseParent.parent.get()).name.get();

const mountedResource = await mounted;
const _mountedEndpoint = await mountedResource.getEndpoint('tcp');
const _mountedHost = await _mountedEndpoint.host.get();
const _mountedPort = await _mountedEndpoint.port.get();
const _mountedUri = await _mountedEndpoint.url.get();
const _mountedConnectionString = await mountedResource.connectionStringExpression.get();

await builder.build().run();
