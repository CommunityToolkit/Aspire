import { chmodSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const loki = await builder.addLoki("loki");
await loki.withHttpHostPort({ port: null });
await loki.withGrpcHostPort({ port: null });
await loki.withDataVolume();

const bindMountPath = mkdtempSync(join(tmpdir(), "loki-"));
if (process.platform !== "win32") {
	chmodSync(bindMountPath, 0o777);
}

const mountedLoki = await builder.addLoki("loki-mounted");
await mountedLoki.withDataBindMount(bindMountPath);
await mountedLoki.withConfigFile(fileURLToPath(new URL("./loki.yaml", import.meta.url)));

const _httpEndpoint = await loki.httpEndpoint();
const _grpcEndpoint = await loki.grpcEndpoint();

await builder.build().run();