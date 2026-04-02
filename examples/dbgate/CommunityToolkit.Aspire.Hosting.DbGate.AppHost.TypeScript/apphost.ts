import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { createBuilder } from './.modules/aspire.js';

const bindMountPath = mkdtempSync(join(tmpdir(), 'dbgate-'));

const builder = await createBuilder();

// addDbGate — create a DbGate resource with an explicit name
const dbgate = await builder.addDbGate({ name: "dbgate" });

// withHostPort — configure a fixed host port
await dbgate.withHostPort({ port: 3310 });

// withDataBindMount — bind mount the data directory
await dbgate.withDataBindMount(bindMountPath, { isReadOnly: false });

// Keep this compile-only: runtime bind+volume mounts would overlap on the same target path.
if (process.env.ASPIRE_COMPILE_ONLY === '1') {
    await dbgate.withDataVolume({ name: "dbgate-data" });
}

// ---- Property access on DbGateContainerResource (ExposeProperties = true) ----
const dbgateResource = await dbgate;
const _primaryEndpoint = await dbgateResource.primaryEndpoint.get();

await builder.build().run();
