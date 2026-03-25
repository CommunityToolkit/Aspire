import { mkdirSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const bindMountRoot = mkdtempSync(join(tmpdir(), 'activemq-polyglot-'));
const artemisDataPath = join(bindMountRoot, 'artemis-data');
const artemisConfPath = join(bindMountRoot, 'artemis-conf');

mkdirSync(artemisDataPath, { recursive: true });
mkdirSync(artemisConfPath, { recursive: true });

const mqPassword = await builder.addParameterWithValue("mq-password", "admin", {
    secret: true
});
const mqUser = await builder.addParameterWithValue("mq-user", "admin", {
    publishValueAsDefault: true
});

// addActiveMQ — ActiveMQ Classic with all parameters
const classic = await builder.addActiveMQ("classic", {
    userName: mqUser,
    password: mqPassword,
    port: 36161,
    scheme: "activemq",
    webPort: 38161
});

// addActiveMQ — minimal overloads with explicit credentials for repeatable startup
const classic2 = await builder.addActiveMQ("classic2", {
    userName: mqUser,
    password: mqPassword
});

// addActiveMQArtemis — Artemis with all parameters
const artemis = await builder.addActiveMQArtemis("artemis", {
    userName: mqUser,
    password: mqPassword,
    port: 36162,
    scheme: "tcp",
    webPort: 38162
});

// addActiveMQArtemis — minimal overloads with explicit credentials for repeatable startup
const artemis2 = await builder.addActiveMQArtemis("artemis2", {
    userName: mqUser,
    password: mqPassword
});

// withDataVolume — fluent chaining on Classic
await classic.withDataVolume({ name: "classic-data" });

// withConfVolume — fluent chaining on Classic
await classic.withConfVolume({ name: "classic-conf" });

// withDataBindMount — bind mount on Artemis
await artemis.withDataBindMount(artemisDataPath);

// withConfBindMount — bind mount on Artemis
await artemis.withConfBindMount(artemisConfPath);

// withDataVolume + withConfVolume — chaining on Artemis
await artemis2.withDataVolume();
await artemis2.withConfVolume();

// ---- Property access on ActiveMQServerResourceBase (ExposeProperties = true) ----
const classicResource = await classic;
const _classicEndpoint = await classicResource.primaryEndpoint.get();
const _classicHost = await classicResource.host.get();
const _classicPort = await classicResource.port.get();
const _classicUri = await classicResource.uriExpression.get();
const _classicCstr = await classicResource.connectionStringExpression.get();

const classic2Resource = await classic2;
const _classic2Host = await classic2Resource.host.get();
const _classic2Port = await classic2Resource.port.get();

const artemisResource = await artemis;
const _artemisEndpoint = await artemisResource.primaryEndpoint.get();
const _artemisHost = await artemisResource.host.get();
const _artemisPort = await artemisResource.port.get();
const _artemisUri = await artemisResource.uriExpression.get();
const _artemisCstr = await artemisResource.connectionStringExpression.get();

const artemis2Resource = await artemis2;
const _artemis2Host = await artemis2Resource.host.get();
const _artemis2Port = await artemis2Resource.port.get();

await builder.build().run();
