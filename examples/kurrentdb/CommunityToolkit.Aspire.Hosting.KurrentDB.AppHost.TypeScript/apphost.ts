import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const bindMountRoot = fileURLToPath(new URL("./data/kurrentdb/", import.meta.url));

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
const volumeBackedResource = await volumeBacked;
const _volumeEndpoint = await volumeBackedResource.getEndpoint("http");
const _volumeHost = await _volumeEndpoint.host.get();
const _volumePort = await _volumeEndpoint.port.get();
const _volumeUri = await _volumeEndpoint.url.get();
const _volumeConnectionString = await volumeBackedResource.connectionStringExpression.get();

const bindMountBackedResource = await bindMountBacked;
const _bindEndpoint = await bindMountBackedResource.getEndpoint("http");
const _bindHost = await _bindEndpoint.host.get();
const _bindPort = await _bindEndpoint.port.get();
const _bindUri = await _bindEndpoint.url.get();
const _bindConnectionString = await bindMountBackedResource.connectionStringExpression.get();

const defaultVolumeBackedResource = await defaultVolumeBacked;
const _defaultVolumeEndpoint = await defaultVolumeBackedResource.getEndpoint("http");
const _defaultVolumeHost = await _defaultVolumeEndpoint.host.get();
const _defaultVolumePort = await _defaultVolumeEndpoint.port.get();
const _defaultVolumeUri = await _defaultVolumeEndpoint.url.get();
const _defaultVolumeConnectionString = await defaultVolumeBackedResource.connectionStringExpression.get();

await builder.build().run();
