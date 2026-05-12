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

// ---- Endpoint access on ActiveMQServerResourceBase ----
const classicResource = await classic;
const _classicEndpoint = await classicResource.getEndpoint("tcp");
const _classicHost = await _classicEndpoint.host();
const _classicPort = await _classicEndpoint.port();
const _classicUri = await _classicEndpoint.url();
const _classicCstr = await classicResource.connectionStringExpression.get();

const classic2Resource = await classic2;
const _classic2Endpoint = await classic2Resource.getEndpoint("tcp");
const _classic2Host = await _classic2Endpoint.host();
const _classic2Port = await _classic2Endpoint.port();

const artemisResource = await artemis;
const _artemisEndpoint = await artemisResource.getEndpoint("tcp");
const _artemisHost = await _artemisEndpoint.host();
const _artemisPort = await _artemisEndpoint.port();
const _artemisUri = await _artemisEndpoint.url();
const _artemisCstr = await artemisResource.connectionStringExpression.get();

const artemis2Resource = await artemis2;
const _artemis2Endpoint = await artemis2Resource.getEndpoint("tcp");
const _artemis2Host = await _artemis2Endpoint.host();
const _artemis2Port = await _artemis2Endpoint.port();

await builder.build().run();
