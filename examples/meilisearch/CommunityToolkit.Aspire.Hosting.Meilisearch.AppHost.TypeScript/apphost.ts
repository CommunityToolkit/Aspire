import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();
const bindMountSource = mkdtempSync(join(tmpdir(), "meilisearch-"));

const masterKey = await builder.addParameter("search-master-key", {
    value: "search-master-key-value",
    secret: true,
});

// addMeilisearch — named master key and explicit port
const meilisearch = await builder.addMeilisearch("search", {
    masterKey,
    port: 7700,
});

// addMeilisearch — defaults
const meilisearchWithDefaults = await builder.addMeilisearch("search-defaults");

// withDataVolume — fluent API on a Meilisearch builder
await meilisearch.withDataVolume({ name: "search-data" });

// withDataBindMount — bind mount on a second Meilisearch builder
await meilisearchWithDefaults.withDataBindMount(bindMountSource);

// ---- Property access on MeilisearchResource (ExposeProperties = true) ----
const _primaryEndpoint = await meilisearch.primaryEndpoint();
const _host = await meilisearch.host();
const _port = await meilisearch.port();
const _uri = await meilisearch.uriExpression();
const _connectionString = await meilisearch.connectionStringExpression();

const _defaultsPrimaryEndpoint =
    await meilisearchWithDefaults.primaryEndpoint();
const _defaultsHost = await meilisearchWithDefaults.host();
const _defaultsPort = await meilisearchWithDefaults.port();
const _defaultsUri = await meilisearchWithDefaults.uriExpression();
const _defaultsConnectionString =
    await meilisearchWithDefaults.connectionStringExpression();

await builder.build().run();
