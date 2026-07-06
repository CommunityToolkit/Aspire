import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const duckdb = await builder.addDuckDB("analytics");

const api = await builder.addProject(
    "api",
    "../CommunityToolkit.Aspire.DuckDB.Api/CommunityToolkit.Aspire.DuckDB.Api.csproj",
);
await api.withReference(duckdb);

await builder.build().run();
