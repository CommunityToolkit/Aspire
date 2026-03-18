import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addActiveMQ — ActiveMQ Classic with all parameters
const mqPassword = await builder.addParameter("mq-password", { secret: true });
const mqUser = await builder.addParameter("mq-user");
const classic = await builder.addActiveMQ("classic", {
    userName: mqUser,
    password: mqPassword,
    port: 61616,
    scheme: "tcp",
    webPort: 8161
});

// addActiveMQ — minimal overload (defaults)
const classic2 = await builder.addActiveMQ("classic2");

// addActiveMQArtemis — Artemis with all parameters
const artemis = await builder.addActiveMQArtemis("artemis", {
    userName: mqUser,
    password: mqPassword,
    port: 61617,
    scheme: "tcp",
    webPort: 8162
});

// addActiveMQArtemis — minimal overload (defaults)
const artemis2 = await builder.addActiveMQArtemis("artemis2");

// withDataVolume — fluent chaining on Classic
await classic.withDataVolume({ name: "classic-data" });

// withConfVolume — fluent chaining on Classic
await classic.withConfVolume({ name: "classic-conf" });

// withDataBindMount — bind mount on Artemis
await artemis.withDataBindMount("/tmp/artemis-data");

// withConfBindMount — bind mount on Artemis
await artemis.withConfBindMount("/tmp/artemis-conf");

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

const artemisResource = await artemis;
const _artemisEndpoint = await artemisResource.primaryEndpoint.get();
const _artemisHost = await artemisResource.host.get();
const _artemisPort = await artemisResource.port.get();
const _artemisUri = await artemisResource.uriExpression.get();
const _artemisCstr = await artemisResource.connectionStringExpression.get();

await builder.build().run();
