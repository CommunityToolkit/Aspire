import { createBuilder } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();

const postgres = await builder.addPostgres("postgres");
const db = await postgres.addDatabase("n8n-db");

const n8n = await builder.addN8n("n8n");
await n8n.withPostgresDatabase(db);

await builder.build().run();