import { mkdirSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();
const bindMountRoot = mkdtempSync(join(tmpdir(), "activemq-polyglot-"));
const artemisDataPath = join(bindMountRoot, "artemis-data");
const artemisConfPath = join(bindMountRoot, "artemis-conf");

mkdirSync(artemisDataPath, { recursive: true });
mkdirSync(artemisConfPath, { recursive: true });

const mqPassword = await builder.addParameter("mq-password", {
    value: "admin",
    secret: true,
});
const mqUser = await builder.addParameter("mq-user", {
    value: "admin",
    publishValueAsDefault: true,
});

// addActiveMQ — ActiveMQ Classic with all parameters
const classic = await builder.addActiveMQ("classic", {
    userName: mqUser,
    password: mqPassword,
    port: 36161,
    scheme: "activemq",
    webPort: 38161,
});

// addActiveMQ — minimal overloads with explicit credentials for repeatable startup
const classic2 = await builder.addActiveMQ("classic2", {
    userName: mqUser,
    password: mqPassword,
});

// addActiveMQArtemis — Artemis with all parameters
const artemis = await builder.addActiveMQArtemis("artemis", {
    userName: mqUser,
    password: mqPassword,
    port: 36162,
    scheme: "tcp",
    webPort: 38162,
});

// addActiveMQArtemis — minimal overloads with explicit credentials for repeatable startup
const artemis2 = await builder.addActiveMQArtemis("artemis2", {
    userName: mqUser,
    password: mqPassword,
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
const _classicEndpoint = await classic.primaryEndpoint();
const _classicHost = await classic.host();
const _classicPort = await classic.port();
const _classicUri = await classic.uriExpression();
const _classicCstr = await classic.connectionStringExpression();

const _classic2Endpoint = await classic2.primaryEndpoint();
const _classic2Host = await classic2.host();
const _classic2Port = await classic2.port();

const _artemisEndpoint = await artemis.primaryEndpoint();
const _artemisHost = await artemis.host();
const _artemisPort = await artemis.port();
const _artemisUri = await artemis.uriExpression();
const _artemisCstr = await artemis.connectionStringExpression();

const _artemis2Endpoint = await artemis2.primaryEndpoint();
const _artemis2Host = await artemis2.host();
const _artemis2Port = await artemis2.port();

await builder.build().run();
