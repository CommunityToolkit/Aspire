import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { createBuilder } from "./.modules/aspire.js";

const bindMountPath = mkdtempSync(join(tmpdir(), "lavinmq-"));

const builder = await createBuilder();

// addLavinMQ + withDataVolume
const volumeBroker = await builder.addLavinMQ("volume-broker", {
    amqpPort: 35672,
    managementPort: 35673,
});
await volumeBroker.withDataVolume("lavinmq-volume");

// addLavinMQ + withDataBindMount
const bindBroker = await builder.addLavinMQ("bind-broker", {
    amqpPort: 35674,
    managementPort: 35675,
});
await bindBroker.withDataBindMount(bindMountPath);

// ---- Endpoint access on LavinMQContainerResource ----
const _primaryEndpoint = await volumeBroker.primaryEndpoint();
const _host = await volumeBroker.host();
const _port = await volumeBroker.port();
const _uriExpression = await volumeBroker.uriExpression();
const _connectionStringExpression =
    await volumeBroker.connectionStringExpression();

await builder.build().run();
