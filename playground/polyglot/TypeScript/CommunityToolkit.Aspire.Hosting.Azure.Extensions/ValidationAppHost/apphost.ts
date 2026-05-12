import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const storage = await builder.addAzureStorage("storage");

await storage.runAsEmulator({
    configureContainer: async (azurite) => {
        await azurite.withBlobPort(27000);
        await azurite.withQueuePort(27001);
        await azurite.withTablePort(27002);
    }
});

const blobs = await storage.addBlobs("blobs");
await blobs.withAzureStorageExplorer({
    configureContainer: async (explorer) => {
        await explorer.withHostPort({ port: 8068 });

        const _endpoint = await explorer.getEndpoint('http');
        const _host = await _endpoint.host();
        const _port = await _endpoint.port();
    }
});

const queues = await storage.addQueues("queues");
await queues.withAzureStorageExplorer({
    name: "queues-explorer"
});

const tables = await storage.addTables("tables");
await tables.withAzureStorageExplorer({
    configureContainer: async (explorer) => {
        const _endpoint = await explorer.getEndpoint('http');
        const _host = await _endpoint.host();
        const _port = await _endpoint.port();
    },
    name: "tables-explorer"
});

await builder.build().run();
