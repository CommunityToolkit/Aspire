import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

// addDbx — create a Dbx resource with an explicit name
const dbx = await builder.addDbx({ name: "dbx" });

// withHostPort — configure a fixed host port
await dbx.withHostPort({ port: 3310 });

// ---- Property access on DbxContainerResource (ExposeProperties = true) ----
const dbxResource = await dbx;
const _primaryEndpoint = await dbxResource.primaryEndpoint();

await builder.build().run();
