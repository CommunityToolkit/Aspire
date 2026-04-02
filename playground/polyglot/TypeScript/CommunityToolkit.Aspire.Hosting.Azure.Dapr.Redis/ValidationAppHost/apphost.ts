import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const managedIdentityRedis = await builder.addAzureManagedRedisForDapr('managed-redis', {
    runAsContainer: true
});

const accessKeyRedis = await builder.addAzureManagedRedisForDapr('accesskey-redis', {
    useAccessKeyAuthentication: true,
    runAsContainer: true
});

const stateStore = await builder.addDaprStateStoreForAzureManagedRedis('statestore');
await stateStore.withReference(accessKeyRedis);

const pubSub = await builder.addDaprPubSubForAzureManagedRedis('pubsub');
await pubSub.withReference(managedIdentityRedis);

await builder.build().run();
