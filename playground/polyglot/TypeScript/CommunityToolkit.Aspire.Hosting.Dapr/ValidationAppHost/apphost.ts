import { createBuilder, type DaprComponentOptions, type ReferenceExpression } from './.modules/aspire.js';

const builder = await createBuilder();

await builder.addDapr({
    configure: async (dapr) => {
        await dapr.daprPath.set('dapr');
        await dapr.enableTelemetry.set(false);

        const configuredDaprPath = await dapr.daprPath.get();
        const telemetryEnabled = await dapr.enableTelemetry.get();

        void [configuredDaprPath, telemetryEnabled];
    }
});

const componentSecret = await builder.addParameter('component-secret', { secret: true });

const cacheBackend = builder
    .addContainer('cache-backend', 'redis')
    .withEndpoint({ name: 'tcp', targetPort: 6379 });

const cacheEndpoint = await cacheBackend.getEndpoint('tcp');

let cacheConnectionExpression!: ReferenceExpression;
await builder.addConnectionStringBuilder('cache-connection', async (connectionString) => {
    await connectionString.appendLiteral('redis://');
    await connectionString.appendValueProvider(cacheEndpoint);
    cacheConnectionExpression = await connectionString.build();
});

const customComponent = builder.addDaprComponent('custom-binding', 'bindings.http', {
    componentOptions: { localPath: './components/binding.yaml' }
});

const pubsub = builder
    .addDaprPubSub('pubsub', {
        componentOptions: { localPath: './components/pubsub.yaml' }
    })
    .withMetadata('consumerId', 'checkout')
    .withMetadataEndpoint('redisHost', cacheEndpoint)
    .withMetadataReferenceExpression('redisAddress', cacheConnectionExpression)
    .withMetadataParameter('redisPassword', componentSecret);

const stateStore = builder
    .addDaprStateStore('statestore', {
        componentOptions: { localPath: './components/statestore.yaml' }
    })
    .withMetadata('actorStateStore', 'true');

const frontend = builder
    .addContainer('frontend', 'nginx')
    .withEndpoint({ name: 'http', scheme: 'http', targetPort: 80 })
    .withDaprSidecar({
        sidecarOptions: {
            appId: 'frontend',
            appPort: 80,
            appProtocol: 'http',
            daprHttpPort: 3500,
            enableApiLogging: true,
            resourcesPaths: ['./components']
        }
    });

const api = builder
    .addContainer('api', 'nginx')
    .withEndpoint({ name: 'http', scheme: 'http', targetPort: 80 })
    .configureDaprSidecar(async (sidecar) => {
        await sidecar.withOptions({
            appId: 'api',
            appPort: 80,
            appProtocol: 'http',
            daprGrpcPort: 50001,
            enableApiLogging: true,
            logLevel: 'debug',
            resourcesPaths: ['./components']
        });
        await sidecar.withReference(await customComponent);
        await sidecar.withReference(await pubsub);
        await sidecar.withReference(await stateStore);
    });

const customComponentResource = await customComponent;
const pubsubResource = await pubsub;
const stateStoreResource = await stateStore;

const customComponentType = await customComponentResource.type.get();
const customComponentSettings: DaprComponentOptions = await customComponentResource.options.get();
const pubsubType = await pubsubResource.type.get();
const pubsubSettings: DaprComponentOptions = await pubsubResource.options.get();
const stateStoreType = await stateStoreResource.type.get();
const stateStoreSettings: DaprComponentOptions = await stateStoreResource.options.get();

await Promise.all([frontend, api]);

void [
    customComponentType,
    customComponentSettings.localPath,
    pubsubType,
    pubsubSettings.localPath,
    stateStoreType,
    stateStoreSettings.localPath
];

await builder.build().run();
