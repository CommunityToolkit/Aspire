import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const bindMountSource = mkdtempSync(join(tmpdir(), 'meilisearch-'));

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
const meilisearchResource = meilisearch;
const _primaryEndpoint = await meilisearchResource.getEndpoint('http');
const _host = await _primaryEndpoint.host.get();
const _port = await _primaryEndpoint.port.get();
const _uri = await _primaryEndpoint.url.get();
const _connectionString = await meilisearchResource.connectionStringExpression.get();

const meilisearchWithDefaultsResource = meilisearchWithDefaults;
const _defaultsPrimaryEndpoint = await meilisearchWithDefaultsResource.getEndpoint('http');
const _defaultsHost = await _defaultsPrimaryEndpoint.host.get();
const _defaultsPort = await _defaultsPrimaryEndpoint.port.get();
const _defaultsUri = await _defaultsPrimaryEndpoint.url.get();
const _defaultsConnectionString = await meilisearchWithDefaultsResource.connectionStringExpression.get();

await builder.build().run();
