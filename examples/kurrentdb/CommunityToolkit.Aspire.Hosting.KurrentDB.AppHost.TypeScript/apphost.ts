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

// ---- Property access on KurrentDBResource (ExposeProperties = true) ----
const volumeBackedResource = await volumeBacked;
const _volumeEndpoint = await volumeBackedResource.primaryEndpoint.get();
const _volumeHost = await volumeBackedResource.host.get();
const _volumePort = await volumeBackedResource.port.get();
const _volumeUri = await volumeBackedResource.uriExpression.get();
const _volumeConnectionString = await volumeBackedResource.connectionStringExpression.get();

const bindMountBackedResource = await bindMountBacked;
const _bindEndpoint = await bindMountBackedResource.primaryEndpoint.get();
const _bindHost = await bindMountBackedResource.host.get();
const _bindPort = await bindMountBackedResource.port.get();
const _bindUri = await bindMountBackedResource.uriExpression.get();
const _bindConnectionString = await bindMountBackedResource.connectionStringExpression.get();

const defaultVolumeBackedResource = await defaultVolumeBacked;
const _defaultVolumeEndpoint = await defaultVolumeBackedResource.primaryEndpoint.get();
const _defaultVolumeHost = await defaultVolumeBackedResource.host.get();
const _defaultVolumePort = await defaultVolumeBackedResource.port.get();
const _defaultVolumeUri = await defaultVolumeBackedResource.uriExpression.get();
const _defaultVolumeConnectionString = await defaultVolumeBackedResource.connectionStringExpression.get();

await builder.build().run();
