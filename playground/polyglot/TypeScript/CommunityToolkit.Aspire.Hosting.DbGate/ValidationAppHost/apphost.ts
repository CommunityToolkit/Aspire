import { mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { createBuilder } from './.modules/aspire.js';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const bindMountPath = join(currentDirectory, 'dbgate-data');
mkdirSync(bindMountPath, { recursive: true });

const builder = await createBuilder();

// addDbGate — create a DbGate resource with an explicit name
const dbgate = await builder.addDbGate({ name: "dbgate" });

// withHostPort — configure a fixed host port
await dbgate.withHostPort({ port: 3310 });

// withDataBindMount — bind mount the data directory
await dbgate.withDataBindMount(bindMountPath, { isReadOnly: false });

// withDataVolume — keep compile-time coverage without overlapping runtime mounts
if (process.env.ASPIRE_RUNTIME_SMOKE !== '1') {
    await dbgate.withDataVolume({ name: "dbgate-data" });
}

// ---- Property access on DbGateContainerResource (ExposeProperties = true) ----
const dbgateResource = await dbgate;
const _primaryEndpoint = await dbgateResource.primaryEndpoint.get();

await builder.build().run();
