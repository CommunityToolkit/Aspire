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
const _volumeEndpoint = await volumeBacked.getEndpoint("http");
const _volumeHost = await _volumeEndpoint.host();
const _volumePort = await _volumeEndpoint.port();
const _volumeUri = await _volumeEndpoint.url();
const _volumeConnectionString = await volumeBacked.connectionStringExpression();

const _bindEndpoint = await bindMountBacked.getEndpoint("http");
const _bindHost = await _bindEndpoint.host();
const _bindPort = await _bindEndpoint.port();
const _bindUri = await _bindEndpoint.url();
const _bindConnectionString =
    await bindMountBacked.connectionStringExpression();

const _defaultVolumeEndpoint = await defaultVolumeBacked.getEndpoint("http");
const _defaultVolumeHost = await _defaultVolumeEndpoint.host();
const _defaultVolumePort = await _defaultVolumeEndpoint.port();
const _defaultVolumeUri = await _defaultVolumeEndpoint.url();
const _defaultVolumeConnectionString =
    await defaultVolumeBacked.connectionStringExpression();

await builder.build().run();
