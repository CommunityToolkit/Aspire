import { mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const appHostDir = dirname(fileURLToPath(import.meta.url));
const bindMountSource = join(appHostDir, 'meilisearch-data');
mkdirSync(bindMountSource, { recursive: true });

const masterKey = await builder.addParameterWithValue('search-master-key', 'search-master-key-value', { secret: true });

// addMeilisearch — named master key and explicit port
const meilisearch = await builder.addMeilisearch('search', {
    masterKey,
    port: 7700
});

// addMeilisearch — defaults
const meilisearchWithDefaults = await builder.addMeilisearch('search-defaults');

// withDataVolume — fluent API on a Meilisearch builder
await meilisearch.withDataVolume({ name: 'search-data' });

// withDataBindMount — bind mount on a second Meilisearch builder
await meilisearchWithDefaults.withDataBindMount(bindMountSource);

// ---- Property access on MeilisearchResource (ExposeProperties = true) ----
const meilisearchResource = await meilisearch;
const _primaryEndpoint = await meilisearchResource.primaryEndpoint.get();
const _host = await meilisearchResource.host.get();
const _port = await meilisearchResource.port.get();
const _uri = await meilisearchResource.uriExpression.get();
const _connectionString = await meilisearchResource.connectionStringExpression.get();

await builder.build().run();
