import { chmodSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const tempo = await builder.addTempo("tempo");
await tempo.withHttpHostPort({ port: null });
await tempo.withGrpcHostPort({ port: null });
await tempo.withOtlpGrpcHostPort({ port: null });
await tempo.withOtlpHttpHostPort({ port: null });
await tempo.withDataVolume();

const bindMountPath = mkdtempSync(join(tmpdir(), "tempo-"));
if (process.platform !== "win32") {
    chmodSync(bindMountPath, 0o777);
}

const mountedTempo = await builder.addTempo("tempo-mounted");
await mountedTempo.withDataBindMount(bindMountPath);
await mountedTempo.withConfigFile(fileURLToPath(new URL("./tempo.yaml", import.meta.url)));

const _httpEndpoint = await tempo.httpEndpoint();
const _grpcEndpoint = await tempo.grpcEndpoint();
const _otlpGrpcEndpoint = await tempo.otlpGrpcEndpoint();
const _otlpHttpEndpoint = await tempo.otlpHttpEndpoint();

await builder.build().run();