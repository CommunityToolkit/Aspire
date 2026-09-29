import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const mqtt = await builder.addMosquitto("mqtt");
await mqtt.withDataVolume({ name: "mosquitto-data" });

const _primaryEndpoint = await mqtt.primaryEndpoint();
const _host = await mqtt.host();
const _port = await mqtt.port();
const _connectionString = await mqtt.connectionStringExpression();
const _uri = await mqtt.uriExpression();

await builder.build().run();
