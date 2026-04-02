import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { createBuilder } from './.modules/aspire.js';

const bindMountPath = mkdtempSync(join(tmpdir(), 'lavinmq-'));

const builder = await createBuilder();

// addLavinMQ + withDataVolume
const volumeBroker = await builder.addLavinMQ("volume-broker", {
    amqpPort: 35672,
    managementPort: 35673
});
await volumeBroker.withDataVolume("lavinmq-volume");

// addLavinMQ + withDataBindMount
const bindBroker = await builder.addLavinMQ("bind-broker", {
    amqpPort: 35674,
    managementPort: 35675
});
await bindBroker.withDataBindMount(bindMountPath);

// ---- Property access on LavinMQContainerResource (ExposeProperties = true) ----
const volumeBrokerResource = volumeBroker;
const _primaryEndpoint = await volumeBrokerResource.primaryEndpoint.get();
const _host = await volumeBrokerResource.host.get();
const _port = await volumeBrokerResource.port.get();
const _uriExpression = await volumeBrokerResource.uriExpression.get();
const _connectionStringExpression = await volumeBrokerResource.connectionStringExpression.get();

await builder.build().run();
