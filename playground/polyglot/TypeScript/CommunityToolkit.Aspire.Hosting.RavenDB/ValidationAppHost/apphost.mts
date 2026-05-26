import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const validationMountRoot = resolve('.validation-mounts');
const dataBindMountPath = resolve(validationMountRoot, 'data');
const logBindMountPath = resolve(validationMountRoot, 'logs');
// A second RavenDB server is useful for type coverage, but it is opt-in at runtime because
// the exported surface does not currently expose a way to change the default TCP port.
const validateSecondResource = process.env.RAVENDB_VALIDATE_SECOND_RESOURCE === 'true';

mkdirSync(dataBindMountPath, { recursive: true });
mkdirSync(logBindMountPath, { recursive: true });

const raven = await builder.addRavenDB('ravendb');
const ordersDatabase = await raven.addDatabase('orders', {
    databaseName: 'Orders',
    ensureCreated: true
});

await raven.withDataVolume({
    name: 'ravendb-data'
});
await raven.withLogVolume({
    name: 'ravendb-logs',
    isReadOnly: false
});

const ravenResource = await raven;
const _primaryEndpoint = await ravenResource.primaryEndpoint.get();
const _host = await ravenResource.host.get();
const _port = await ravenResource.port.get();
const _tcpEndpoint = await ravenResource.tcpEndpoint.get();
const _uri = await ravenResource.uriExpression.get();
const _connectionString = await ravenResource.connectionStringExpression.get();

const ordersDatabaseResource = await ordersDatabase;
const _databaseName: string = await ordersDatabaseResource.databaseName.get();
const _databaseConnectionString = await ordersDatabaseResource.connectionStringExpression.get();
const ordersDatabaseParent = await ordersDatabaseResource.parent.get();
const _databaseParentHost = await ordersDatabaseParent.host.get();
const _databaseParentConnectionString = await ordersDatabaseParent.connectionStringExpression.get();

if (validateSecondResource) {
    const ravenWithBindMounts = await builder.addRavenDB('ravendb-bind');
    const inventoryDatabase = await ravenWithBindMounts.addDatabase('inventory');

    await ravenWithBindMounts.withDataBindMount(dataBindMountPath);
    await ravenWithBindMounts.withLogBindMount(logBindMountPath);

    const inventoryDatabaseResource = await inventoryDatabase;
    const _defaultDatabaseName: string = await inventoryDatabaseResource.databaseName.get();
    const _defaultDatabaseConnectionString = await inventoryDatabaseResource.connectionStringExpression.get();
}

await builder.build().run();
