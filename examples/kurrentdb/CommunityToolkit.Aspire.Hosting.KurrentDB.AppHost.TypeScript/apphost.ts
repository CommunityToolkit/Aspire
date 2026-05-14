import { mkdirSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();
const bindMountRoot = fileURLToPath(
    new URL("./data/kurrentdb/", import.meta.url),
);

mkdirSync(bindMountRoot, { recursive: true });

// addKurrentDB — named port overload
const volumeBacked = await builder.addKurrentDB("kurrentdb", { port: 22113 });

// addKurrentDB — default overload
const bindMountBacked = await builder.addKurrentDB("kurrentdb2");

// addKurrentDB — default overload for exercising automatic volume naming
const defaultVolumeBacked = await builder.addKurrentDB("kurrentdb3");

// withDataVolume — named volume overload
await volumeBacked.withDataVolume({ name: "kurrentdb-data" });

// withDataBindMount — bind mount overload
await bindMountBacked.withDataBindMount(bindMountRoot);

// withDataVolume — default overload
await defaultVolumeBacked.withDataVolume();

// ---- Endpoint access on KurrentDBResource ----
const _volumeEndpoint = await volumeBacked.primaryEndpoint();
const _volumeHost = await volumeBacked.host();
const _volumePort = await volumeBacked.port();
const _volumeUri = await volumeBacked.uriExpression();
const _volumeConnectionString = await volumeBacked.connectionStringExpression();

const _bindEndpoint = await bindMountBacked.primaryEndpoint();
const _bindHost = await bindMountBacked.host();
const _bindPort = await bindMountBacked.port();
const _bindUri = await bindMountBacked.uriExpression();
const _bindConnectionString =
    await bindMountBacked.connectionStringExpression();

const _defaultVolumeEndpoint = await defaultVolumeBacked.primaryEndpoint();
const _defaultVolumeHost = await defaultVolumeBacked.host();
const _defaultVolumePort = await defaultVolumeBacked.port();
const _defaultVolumeUri = await defaultVolumeBacked.uriExpression();
const _defaultVolumeConnectionString =
    await defaultVolumeBacked.connectionStringExpression();

await builder.build().run();
