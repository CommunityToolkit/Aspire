import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const redpanda = await builder.addRedPanda("redpanda");
await redpanda.withDataVolume({ name: "redpanda-data" });

const _primaryEndpoint = await redpanda.primaryEndpoint();
const _host = await redpanda.host();
const _port = await redpanda.port();
const _connectionString = await redpanda.connectionStringExpression();

const _pinnedRedpanda = await builder.addRedPanda("redpanda-pinned", {
    port: 9092,
});

await builder.build().run();
