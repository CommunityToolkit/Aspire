import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const chroma = await builder.addChroma("chroma");
await chroma.withDataVolume({ name: "chroma-data" });

const _primaryEndpoint = await chroma.primaryEndpoint();
const _host = await chroma.host();
const _port = await chroma.port();
const _connectionString = await chroma.connectionStringExpression();
const _uri = await chroma.uriExpression();

await builder.build().run();
